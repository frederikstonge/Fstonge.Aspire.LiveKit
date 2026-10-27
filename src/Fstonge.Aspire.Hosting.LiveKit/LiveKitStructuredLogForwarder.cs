using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace Fstonge.Aspire.Hosting.LiveKit;

internal sealed class LiveKitStructuredLogForwarder(
    LiveKitServerResource resource,
    ResourceLoggerService resourceLoggerService,
    IConfiguration configuration,
    ILogger<LiveKitStructuredLogForwarder> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpointValue = configuration["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"]
            ?? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"];
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException(
                "LiveKit structured log forwarding is enabled, but no valid OTLP endpoint is configured. "
                + "Set OTEL_EXPORTER_OTLP_LOGS_ENDPOINT, OTEL_EXPORTER_OTLP_ENDPOINT, "
                + "or ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL.");
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenTelemetry().WithLogging(
            logging =>
            {
                logging.SetResourceBuilder(
                    ResourceBuilder.CreateDefault().AddService(resource.Name));
                logging.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = endpoint;
                    exporter.Headers = GetHeaders(configuration);
                    exporter.Protocol = GetProtocol(configuration, endpointValue!);
                });
            },
            options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;
            });

        using var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var livekitLogger = loggerFactory.CreateLogger($"LiveKit.{resource.Name}");
        livekitLogger.LogInformation(
            "Structured log forwarding started for LiveKit resource {ResourceName}",
            resource.Name);
        logger.LogInformation(
            "Forwarding structured logs for LiveKit resource {ResourceName} to OTLP endpoint {OtlpEndpoint}",
            resource.Name,
            endpoint);

        await foreach (var entries in resourceLoggerService.WatchAsync(resource).WithCancellation(stoppingToken))
        {
            foreach (var entry in entries)
            {
                ForwardLogEntry(entry.Content, entry.IsErrorMessage, entry.LineNumber, livekitLogger);
            }
        }
    }

    private static string? GetHeaders(IConfiguration configuration)
    {
        return configuration["OTEL_EXPORTER_OTLP_LOGS_HEADERS"]
            ?? configuration["OTEL_EXPORTER_OTLP_HEADERS"]
            ?? (configuration["AppHost:OtlpApiKey"] is { } apiKey
                ? $"x-otlp-api-key={apiKey}"
                : null);
    }

    private static OtlpExportProtocol GetProtocol(IConfiguration configuration, string endpoint)
    {
        var protocol = configuration["OTEL_EXPORTER_OTLP_LOGS_PROTOCOL"]
            ?? configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];
        if (protocol is not null)
        {
            return protocol.ToLowerInvariant() switch
            {
                "grpc" => OtlpExportProtocol.Grpc,
                "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
                _ => throw new InvalidOperationException(
                    $"Unsupported OTLP protocol '{protocol}'. Use 'grpc' or 'http/protobuf'.")
            };
        }

        return endpoint.EndsWith("/v1/logs", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.HttpProtobuf
            : OtlpExportProtocol.Grpc;
    }

    private void ForwardLogEntry(string content, bool isErrorMessage, int lineNumber, ILogger livekitLogger)
    {
        DateTimeOffset? resourceLogTimestamp = null;
        var jsonStart = content.IndexOf('{');
        if (jsonStart > 0
            && DateTimeOffset.TryParse(
                content.AsSpan(0, jsonStart).Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsedTimestamp))
        {
            resourceLogTimestamp = parsedTimestamp;
            content = content[jsonStart..];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            logger.LogWarning(
                "Skipping non-JSON log entry from LiveKit resource {ResourceName}: {LogEntry}",
                resource.Name,
                content);
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.LogWarning(
                    "Skipping non-object JSON log entry from LiveKit resource {ResourceName}: {LogEntry}",
                    resource.Name,
                    content);
                return;
            }

            var message = root.TryGetProperty("msg", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String
                    ? messageElement.GetString()
                    : content;
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["livekit.log.line_number"] = lineNumber
            };

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is "msg" or "level" or "time")
                {
                    continue;
                }

                properties[property.Name] = GetPropertyValue(property.Value);
            }

            if (root.TryGetProperty("time", out var jsonTimestamp))
            {
                properties["livekit.timestamp"] = GetPropertyValue(jsonTimestamp);
            }
            else if (resourceLogTimestamp is { } timestamp)
            {
                properties["livekit.timestamp"] = timestamp;
            }

            using (livekitLogger.BeginScope(properties))
            {
                livekitLogger.Log(GetLogLevel(root, isErrorMessage), "{LiveKitMessage}", message);
            }
        }
    }

    private static LogLevel GetLogLevel(JsonElement root, bool isErrorMessage)
    {
        if (!root.TryGetProperty("level", out var level) || level.ValueKind != JsonValueKind.String)
        {
            return isErrorMessage ? LogLevel.Error : LogLevel.Information;
        }

        return level.GetString()?.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "fatal" or "panic" => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }

    private static object? GetPropertyValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }
}
