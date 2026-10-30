using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace Aspire.Hosting;

internal sealed class LiveKitStructuredLogForwarder(
    IResource resource,
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
                $"Structured log forwarding is enabled for resource '{resource.Name}', but no valid OTLP endpoint is configured. "
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
        logger.LogInformation(
            "Forwarding structured logs for resource {ResourceName} to OTLP endpoint {OtlpEndpoint}",
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

    private void ForwardLogEntry(string content, bool isErrorMessage, int lineNumber, ILogger resourceLogger)
    {
        DateTimeOffset? resourceLogTimestamp = null;
        var timestampEnd = content.IndexOf(' ');
        if (timestampEnd > 0
            && DateTimeOffset.TryParse(
                content.AsSpan(0, timestampEnd),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsedTimestamp))
        {
            resourceLogTimestamp = parsedTimestamp;
            content = content[(timestampEnd + 1)..];
        }

        if (TryForwardTabDelimitedLog(content, lineNumber, resourceLogTimestamp, resourceLogger))
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            ForwardPlainTextLog(content, isErrorMessage, lineNumber, resourceLogTimestamp, resourceLogger);
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                ForwardPlainTextLog(content, isErrorMessage, lineNumber, resourceLogTimestamp, resourceLogger);
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
            if (isErrorMessage)
            {
                properties["aspire.log.is_error_stream"] = true;
            }

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

            using (resourceLogger.BeginScope(properties))
            {
                resourceLogger.Log(GetLogLevel(root), "{ResourceLogMessage}", message);
            }
        }
    }

    private static bool TryForwardTabDelimitedLog(
        string content,
        int lineNumber,
        DateTimeOffset? outerTimestamp,
        ILogger resourceLogger)
    {
        var fields = content.Split('\t', 6, StringSplitOptions.None);
        if (fields.Length < 5
            || !DateTimeOffset.TryParse(
                fields[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var timestamp)
            || GetLogLevel(fields[1]) is not { } logLevel)
        {
            return false;
        }

        JsonDocument? attributesDocument = null;
        if (fields.Length == 6)
        {
            try
            {
                attributesDocument = JsonDocument.Parse(fields[5]);
            }
            catch (JsonException)
            {
                return false;
            }

            if (attributesDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                attributesDocument.Dispose();
                return false;
            }
        }

        using (attributesDocument)
        {
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["livekit.log.line_number"] = lineNumber,
                ["livekit.logger"] = fields[2],
                ["livekit.caller"] = fields[3],
                ["livekit.timestamp"] = timestamp
            };
            if (outerTimestamp is { } aspireTimestamp)
            {
                properties["aspire.timestamp"] = aspireTimestamp;
            }

            if (attributesDocument is not null)
            {
                foreach (var property in attributesDocument.RootElement.EnumerateObject())
                {
                    properties[property.Name] = GetPropertyValue(property.Value);
                }
            }

            using (resourceLogger.BeginScope(properties))
            {
                resourceLogger.Log(logLevel, "{ResourceLogMessage}", fields[4]);
            }
        }

        return true;
    }

    private static void ForwardPlainTextLog(
        string content,
        bool isErrorMessage,
        int lineNumber,
        DateTimeOffset? timestamp,
        ILogger resourceLogger)
    {
        var properties = new Dictionary<string, object?>
        {
            ["livekit.log.line_number"] = lineNumber,
            ["aspire.log.is_error_stream"] = isErrorMessage
        };
        if (timestamp is { } logTimestamp)
        {
            properties["livekit.timestamp"] = logTimestamp;
        }

        using (resourceLogger.BeginScope(properties))
        {
            resourceLogger.Log(
                GetPlainTextLogLevel(content, isErrorMessage),
                "{ResourceLogMessage}",
                content);
        }
    }

    private static LogLevel GetPlainTextLogLevel(string content, bool isErrorMessage)
    {
        var prefix = content.AsSpan().TrimStart();
        if (prefix.Contains('\t'))
        {
            var tabFields = prefix.ToString().Split('\t', 5, StringSplitOptions.None);
            foreach (var field in tabFields)
            {
                if (GetLogLevel(field) is { } tabLogLevel)
                {
                    return tabLogLevel;
                }
            }
        }

        if (prefix.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracket = prefix.IndexOf(']');
            if (closingBracket > 1)
            {
                prefix = prefix[1..closingBracket];
            }
        }
        else
        {
            var separator = prefix.IndexOfAny(' ', '\t', ':');
            if (separator > 0)
            {
                prefix = prefix[..separator];
            }
        }

        return GetLogLevel(prefix.ToString()) ?? LogLevel.Information;
    }

    private static LogLevel GetLogLevel(JsonElement root)
    {
        if (!root.TryGetProperty("level", out var level) || level.ValueKind != JsonValueKind.String)
        {
            return LogLevel.Information;
        }

        return GetLogLevel(level.GetString()) ?? LogLevel.Information;
    }

    private static LogLevel? GetLogLevel(string? level)
    {
        return level?.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "fatal" or "panic" => LogLevel.Critical,
            _ => null
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
