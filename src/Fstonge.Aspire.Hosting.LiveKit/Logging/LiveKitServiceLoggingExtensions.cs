using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

public static class LiveKitServiceLoggingExtensions
{
        /// <summary>
    /// Configures JSON formatted logging for a LiveKit container resource.
    /// </summary>
    /// <typeparam name="TResource">The type of the LiveKit container resource.</typeparam>
    /// <param name="resourceBuilder">The LiveKit container resource builder.</param>
    /// <returns>The configured resource builder.</returns>
    [AspireExportIgnore(Reason = "Use resource-specific logging exports in generated AppHosts.")]
    public static IResourceBuilder<TResource> WithJsonLogging<TResource>(
        this IResourceBuilder<TResource> resourceBuilder)
        where TResource : LiveKitContainerResource
    {
        ArgumentNullException.ThrowIfNull(resourceBuilder);

        resourceBuilder.Resource.JsonLoggingEnabled = true;
        return resourceBuilder;
    }

    /// <summary>
    /// Enables forwarding structured logs from the LiveKit container to the configured OpenTelemetry (OTLP) log collector.
    /// </summary>
    /// <typeparam name="TResource">The type of the LiveKit container resource.</typeparam>
    /// <param name="resourceBuilder">The LiveKit container resource builder.</param>
    /// <returns>The configured resource builder.</returns>
    [AspireExportIgnore(Reason = "Use resource-specific logging exports in generated AppHosts.")]
    public static IResourceBuilder<TResource> WithStructuredLogs<TResource>(
        this IResourceBuilder<TResource> resourceBuilder)
        where TResource : LiveKitContainerResource
    {
        ArgumentNullException.ThrowIfNull(resourceBuilder);

        var resource = resourceBuilder.Resource;
        if (!resource.StructuredLogForwardingEnabled)
        {
            resource.StructuredLogForwardingEnabled = true;
            resource.JsonLoggingEnabled = true;

            resourceBuilder.ApplicationBuilder.Services.AddSingleton<IHostedService>(serviceProvider =>
                new LiveKitStructuredLogForwarder(
                    resource,
                    serviceProvider.GetRequiredService<ResourceLoggerService>(),
                    serviceProvider.GetRequiredService<IConfiguration>(),
                    serviceProvider.GetRequiredService<ILogger<LiveKitStructuredLogForwarder>>()));
        }

        return resourceBuilder;
    }

    [AspireExportIgnore(Reason = "Use resource-specific logging exports in generated AppHosts.")]
    public static IResourceBuilder<TResource> WithAgentStructuredLogs<TResource>(
        this IResourceBuilder<TResource> resourceBuilder)
        where TResource : IResource
    {
        ArgumentNullException.ThrowIfNull(resourceBuilder);

        var resource = resourceBuilder.Resource;
        
        resourceBuilder.ApplicationBuilder.Services.AddSingleton<IHostedService>(serviceProvider =>
            new LiveKitStructuredLogForwarder(
                resource,
                serviceProvider.GetRequiredService<ResourceLoggerService>(),
                serviceProvider.GetRequiredService<IConfiguration>(),
                serviceProvider.GetRequiredService<ILogger<LiveKitStructuredLogForwarder>>()));
    

        return resourceBuilder;
    }
}