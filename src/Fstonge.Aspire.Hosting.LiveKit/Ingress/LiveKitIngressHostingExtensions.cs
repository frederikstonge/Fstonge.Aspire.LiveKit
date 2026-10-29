using Aspire.Hosting.ApplicationModel;
using System.Globalization;
using System.Net.Sockets;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for registering and configuring LiveKit resources with .NET Aspire.
/// </summary>
public static class LiveKitIngressHostingExtensions
{
    /// <summary>
    /// Adds a LiveKit Ingress service container to the distributed application.
    /// </summary>
    /// <param name="livekit">The parent LiveKit server resource builder.</param>
    /// <param name="name">The name of the Ingress resource.</param>
    /// <param name="redis">The Redis resource used for coordination.</param>
    /// <param name="rtmpBaseUrl">The public base URL for RTMP ingress.</param>
    /// <param name="whipBaseUrl">The public base URL for WHIP ingress.</param>
    /// <param name="version">The container image tag version. Defaults to <c>"latest"</c>.</param>
    /// <param name="rtmpPort">The TCP port for RTMP ingress. Defaults to 1935.</param>
    /// <param name="whipPort">The HTTP port for WHIP signaling. Defaults to 8080.</param>
    /// <param name="whipUdpPort">The UDP port for WHIP media transport. Defaults to 7885.</param>
    /// <returns>An <see cref="IResourceBuilder{LiveKitIngressResource}"/> for configuring the Ingress service.</returns>
    [AspireExport]
    public static IResourceBuilder<LiveKitIngressResource> AddIngress(
        this IResourceBuilder<LiveKitServerResource> livekit,
        string name,
        IResourceBuilder<RedisResource> redis,
        string rtmpBaseUrl,
        string whipBaseUrl,
        string? version = null,
        int rtmpPort = 1935,
        int whipPort = 8080,
        int whipUdpPort = 7885)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentException.ThrowIfNullOrWhiteSpace(rtmpBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(whipBaseUrl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rtmpPort);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(whipPort);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(whipUdpPort);
        ValidatePublicUrl(rtmpBaseUrl, "rtmp", "rtmps");
        ValidatePublicUrl(whipBaseUrl, "http", "https");
        if (rtmpPort == whipPort)
        {
            throw new ArgumentException("The RTMP and WHIP TCP listener ports must be different.", nameof(whipPort));
        }

        livekit.WithRedis(redis);

        var ingressResource = new LiveKitIngressResource(name);
        var ingress = livekit.ApplicationBuilder
            .AddResource(ingressResource)
            .WithParentRelationship(livekit.Resource)
            .WithImage("livekit/ingress")
            .WithImageTag(version ?? "latest")
            .WithEndpoint(port: rtmpPort, targetPort: rtmpPort, name: "rtmp", protocol: ProtocolType.Tcp)
            .WithHttpEndpoint(port: whipPort, targetPort: whipPort, name: "whip")
            .WithEndpoint(port: whipUdpPort, targetPort: whipUdpPort, name: "whip-udp", protocol: ProtocolType.Udp)
            .WithEnvironment(context =>
                context.EnvironmentVariables["INGRESS_CONFIG_BODY"] =
                    CreateIngressConfiguration(
                        livekit.Resource,
                        ingressResource,
                        redis,
                        rtmpPort,
                        whipPort,
                        whipUdpPort));

        ingress.WithReference(livekit.GetEndpoint("http"));
        ingress
            .WaitFor(livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(livekit.Resource))
            .WaitFor(livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(redis.Resource));
        livekit.Resource.RtmpBaseUrl = rtmpBaseUrl;
        livekit.Resource.WhipBaseUrl = whipBaseUrl;

        return ingress;
    }

    /// <summary>
    /// Applies typed configuration settings to the LiveKit Ingress service.
    /// </summary>
    /// <param name="ingress">The LiveKit Ingress resource builder.</param>
    /// <param name="configure">A delegate to configure the <see cref="LiveKitIngressConfiguration"/>.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitIngressResource}"/> with the configuration applied.</returns>
    [AspireExportIgnore(Reason = "Use WithIngressAdditionalConfiguration to configure Ingress in generated AppHosts.")]
    public static IResourceBuilder<LiveKitIngressResource> WithIngressConfiguration(
        this IResourceBuilder<LiveKitIngressResource> ingress,
        Action<LiveKitIngressConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(ingress);
        ArgumentNullException.ThrowIfNull(configure);
        var current = ingress.Resource.Configuration;
        var configuration = current is null
            ? new LiveKitIngressConfiguration()
            : new LiveKitIngressConfiguration
            {
                CpuCost = new LiveKitIngressCpuCostConfiguration
                {
                    RtmpCpuCost = current.CpuCost.RtmpCpuCost,
                    WhipCpuCost = current.CpuCost.WhipCpuCost,
                    WhipBypassTranscodingCpuCost = current.CpuCost.WhipBypassTranscodingCpuCost,
                    UrlCpuCost = current.CpuCost.UrlCpuCost,
                    MinIdleRatio = current.CpuCost.MinIdleRatio,
                },
                Logging = new LiveKitServiceLoggingConfiguration { Level = current.Logging.Level },
            };
        configure(configuration);
        _ = configuration.ToYaml(ingress.Resource.JsonLoggingEnabled);
        ingress.Resource.Configuration = configuration;
        return ingress;
    }

    /// <summary>
    /// Appends additional raw YAML configuration to the LiveKit Ingress service configuration.
    /// </summary>
    /// <param name="ingress">The Ingress resource builder.</param>
    /// <param name="yaml">A valid YAML string containing additional top-level settings.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitIngressResource}"/> with additional configuration applied.</returns>
    [AspireExport]
    public static IResourceBuilder<LiveKitIngressResource> WithIngressAdditionalConfiguration(
        this IResourceBuilder<LiveKitIngressResource> ingress,
        string yaml)
    {
        ArgumentNullException.ThrowIfNull(ingress);
        ingress.Resource.AdditionalConfiguration = LiveKitConfigurationYaml.ValidateAdditionalConfiguration(
            yaml,
            nameof(yaml),
            "api_key",
            "api_secret",
            "ws_url",
            "redis",
            "rtmp_port",
            "whip_port",
            "rtc_config",
            "logging",
            "cpu_cost");
        return ingress;
    }

    /// <summary>Enables JSON console logging for a LiveKit Ingress resource.</summary>
    /// <param name="resourceBuilder">The Ingress resource.</param>
    /// <returns>The configured resource.</returns>
    [AspireExport]
    internal static IResourceBuilder<LiveKitIngressResource> WithIngressJsonLogging(
        IResourceBuilder<LiveKitIngressResource> resourceBuilder) =>
        LiveKitServiceLoggingExtensions.WithJsonLogging(resourceBuilder);

    /// <summary>Enables OTLP log forwarding for a LiveKit Ingress resource.</summary>
    /// <param name="resourceBuilder">The Ingress resource.</param>
    /// <returns>The configured resource.</returns>
    [AspireExport]
    internal static IResourceBuilder<LiveKitIngressResource> WithIngressStructuredLogs(
        IResourceBuilder<LiveKitIngressResource> resourceBuilder) =>
        LiveKitServiceLoggingExtensions.WithStructuredLogs(resourceBuilder);

    private static ReferenceExpression CreateIngressConfiguration<TResource>(
        LiveKitServerResource server,
        LiveKitIngressResource resource,
        IResourceBuilder<TResource> redis,
        int rtmpPort,
        int whipPort,
        int whipUdpPort)
        where TResource : IResource, IResourceWithConnectionString =>
        ReferenceExpression.Create(
            $"api_key: \"{server.ApiKey}\"\napi_secret: \"{server.ApiSecret}\"\nws_url: \"ws://{server.Name}:7880\"\n" +
            $"{LiveKitRedisConfiguration.Create(redis)}" +
            $"rtmp_port: {rtmpPort.ToString(CultureInfo.InvariantCulture)}\n" +
            $"whip_port: {whipPort.ToString(CultureInfo.InvariantCulture)}\n" +
            $"rtc_config:\n  udp_port: {whipUdpPort.ToString(CultureInfo.InvariantCulture)}\n" +
            $"{resource.Configuration?.ToYaml(resource.JsonLoggingEnabled) ?? LiveKitHostingHelpers.GetJsonLoggingConfiguration(resource)}" +
            $"{resource.AdditionalConfiguration ?? string.Empty}");

    private static void ValidatePublicUrl(string value, params string[] allowedSchemes)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !allowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The URL must be an absolute {string.Join(" or ", allowedSchemes)} URL.",
                nameof(value));
        }
    }
}
