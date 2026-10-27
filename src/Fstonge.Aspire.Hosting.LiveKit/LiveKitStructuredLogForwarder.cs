using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;

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
            logger.LogError(
                "LiveKit structured log forwarding is enabled, but no valid OTLP endpoint is configured. "
                + "Set OTEL_EXPORTER_OTLP_LOGS_ENDPOINT, OTEL_EXPORTER_OTLP_ENDPOINT, "
                + "or ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL.");
            return;
        }

        using var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;
                options.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = endpoint;
                    if (configuration["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"] is null
                        && configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is null)
                    {
                        exporter.Protocol = OtlpExportProtocol.Grpc;
                    }
                });
            });
        });

        var livekitLogger = loggerFactory.CreateLogger($"LiveKit.{resource.Name}");
        logger.LogInformation("Forwarding structured logs for LiveKit resource {ResourceName}", resource.Name);

        await foreach (var entries in resourceLoggerService.WatchAsync(resource).WithCancellation(stoppingToken))
        {
            foreach (var entry in entries)
            {
                ForwardLogEntry(entry.Content, entry.IsErrorMessage, entry.LineNumber, livekitLogger);
            }
        }
    }

    private void ForwardLogEntry(string content, bool isErrorMessage, int lineNumber, ILogger livekitLogger)
    {
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
                ["service.name"] = resource.Name,
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
