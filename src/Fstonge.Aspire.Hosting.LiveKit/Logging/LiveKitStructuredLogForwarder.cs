using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        var resourceLogger = loggerFactory.CreateLogger($"LiveKit.{resource.Name}");
        var logParser = new LiveKitLogParser();
        logger.LogInformation(
            "Forwarding structured logs for resource {ResourceName} to OTLP endpoint {OtlpEndpoint}",
            resource.Name,
            endpoint);

        await foreach (var entries in resourceLoggerService.WatchAsync(resource).WithCancellation(stoppingToken))
        {
            foreach (var entry in entries)
            {
                var record = logParser.Parse(entry.Content, entry.IsErrorMessage, entry.LineNumber);
                using (resourceLogger.BeginScope(record.Properties))
                {
                    resourceLogger.Log(record.Level, "{ResourceLogMessage}", record.Message);
                }
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
}
