using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Globalization;
using System.Net.Sockets;

namespace Fstonge.Aspire.Hosting.LiveKit;

public static class LiveKitSipHostingExtensions
{
    /// <summary>
    /// Adds a LiveKit SIP service container to the distributed application.
    /// </summary>
    /// <param name="livekit">The parent LiveKit server resource builder.</param>
    /// <param name="name">The name of the SIP resource.</param>
    /// <param name="redis">The Redis resource used for coordination.</param>
    /// <param name="version">The container image tag version. Defaults to <c>"latest"</c>.</param>
    /// <param name="sipPort">The SIP signaling port on UDP and TCP. Defaults to 5060.</param>
    /// <param name="rtpPortStart">The start of the RTP UDP media port range. Defaults to 10000.</param>
    /// <param name="rtpPortEnd">The end of the RTP UDP media port range. Defaults to 20000.</param>
    /// <param name="healthPort">The optional host port for health check endpoints.</param>
    /// <param name="prometheusPort">The optional host port for Prometheus metrics.</param>
    /// <returns>An <see cref="IResourceBuilder{LiveKitSipResource}"/> for configuring the SIP service.</returns>
    [AspireExport]
    public static IResourceBuilder<LiveKitSipResource> AddSip(
        this IResourceBuilder<LiveKitServerResource> livekit,
        string name,
        IResourceBuilder<RedisResource> redis,
        string? version = null,
        int sipPort = 5060,
        int rtpPortStart = 10000,
        int rtpPortEnd = 20000,
        int? healthPort = null,
        int? prometheusPort = null)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sipPort);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rtpPortStart);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rtpPortEnd);
        if (rtpPortEnd < rtpPortStart)
        {
            throw new ArgumentOutOfRangeException(nameof(rtpPortEnd), "The RTP range end must not be less than its start.");
        }

        LiveKitHostingHelpers.ValidateOptionalPort(healthPort, nameof(healthPort));
        LiveKitHostingHelpers.ValidateOptionalPort(prometheusPort, nameof(prometheusPort));
        if (healthPort is not null && healthPort == prometheusPort)
        {
            throw new ArgumentException("The health and Prometheus ports must be different.", nameof(prometheusPort));
        }

        if (sipPort >= rtpPortStart && sipPort <= rtpPortEnd)
        {
            throw new ArgumentException("The SIP signaling port must not be inside the RTP port range.", nameof(sipPort));
        }

        livekit.WithRedis(redis);

        ValidateSipCompanionPort(healthPort, sipPort, rtpPortStart, rtpPortEnd, nameof(healthPort));
        ValidateSipCompanionPort(prometheusPort, sipPort, rtpPortStart, rtpPortEnd, nameof(prometheusPort));

        var resource = new LiveKitSipResource(
            name,
            livekit.Resource,
            sipPort,
            rtpPortStart,
            rtpPortEnd,
            healthPort,
            prometheusPort);
        var sip = livekit.ApplicationBuilder
            .AddResource(resource)
            .WithParentRelationship(livekit.Resource)
            .WithImage("livekit/sip")
            .WithImageTag(version ?? "latest")
            .WithEndpoint(port: sipPort, targetPort: sipPort, name: "sip-udp", protocol: ProtocolType.Udp)
            .WithEndpoint(port: sipPort, targetPort: sipPort, name: "sip-tcp", protocol: ProtocolType.Tcp);

        if (rtpPortStart == rtpPortEnd)
        {
            sip = sip.WithEndpoint(
                port: rtpPortStart,
                targetPort: rtpPortStart,
                name: "rtp-udp",
                protocol: ProtocolType.Udp);
        }

        if (healthPort is { } sipHealthPort)
        {
            sip = sip.WithHttpEndpoint(port: sipHealthPort, targetPort: sipHealthPort, name: "health");
        }

        if (prometheusPort is { } sipPrometheusPort)
        {
            sip = sip.WithHttpEndpoint(port: sipPrometheusPort, targetPort: sipPrometheusPort, name: "prometheus");
        }

        sip
            .WithEnvironment("LIVEKIT_API_KEY", livekit.Resource.ApiKey)
            .WithEnvironment("LIVEKIT_API_SECRET", livekit.Resource.ApiSecret)
            .WithEnvironment("LIVEKIT_WS_URL", $"ws://{livekit.Resource.Name}:7880")
            .WithEnvironment(context =>
                context.EnvironmentVariables["SIP_CONFIG_BODY"] = CreateSipConfiguration(resource, redis));

        sip
            .WaitFor(livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(livekit.Resource))
            .WaitFor(livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(redis.Resource));

        return sip;
    }

    /// <summary>
    /// Configures SIP over TLS (SIPS) on TCP with specified certificate and private key files.
    /// </summary>
    /// <param name="sip">The SIP resource builder.</param>
    /// <param name="certificateFile">The path to the TLS certificate file.</param>
    /// <param name="keyFile">The path to the TLS private key file.</param>
    /// <param name="tlsPort">The TCP port for SIP TLS signaling. Defaults to 5061.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitSipResource}"/> with TLS enabled.</returns>
    [AspireExport]
    public static IResourceBuilder<LiveKitSipResource> WithSipTls(
        this IResourceBuilder<LiveKitSipResource> sip,
        string certificateFile,
        string keyFile,
        int tlsPort = 5061)
    {
        ArgumentNullException.ThrowIfNull(sip);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFile);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tlsPort);
        if (tlsPort == sip.Resource.SipPort
            || (tlsPort >= sip.Resource.RtpPortStart && tlsPort <= sip.Resource.RtpPortEnd)
            || sip.Resource.HealthPort == tlsPort
            || sip.Resource.PrometheusPort == tlsPort)
        {
            throw new ArgumentException("The SIP TLS port must be outside the SIP and RTP listener ports.", nameof(tlsPort));
        }

        if (sip.Resource.TlsPort is not null)
        {
            throw new InvalidOperationException("SIP TLS has already been configured.");
        }

        sip.Resource.TlsPort = tlsPort;
        sip.Resource.TlsCertificateFile = certificateFile;
        sip.Resource.TlsKeyFile = keyFile;
        return sip.WithEndpoint(port: tlsPort, targetPort: tlsPort, name: "sip-tls", protocol: ProtocolType.Tcp);
    }

    /// <summary>
    /// Applies typed configuration settings to the LiveKit SIP service.
    /// </summary>
    /// <param name="sip">The LiveKit SIP resource builder.</param>
    /// <param name="configure">A delegate to configure the <see cref="LiveKitSipConfiguration"/>.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitSipResource}"/> with the configuration applied.</returns>
    [AspireExportIgnore(Reason = "Use WithSipAdditionalConfiguration to configure SIP in generated AppHosts.")]
    public static IResourceBuilder<LiveKitSipResource> WithSipConfiguration(
        this IResourceBuilder<LiveKitSipResource> sip,
        Action<LiveKitSipConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(sip);
        ArgumentNullException.ThrowIfNull(configure);
        var current = sip.Resource.Configuration;
        var configuration = current is null
            ? new LiveKitSipConfiguration()
            : new LiveKitSipConfiguration
            {
                ClusterId = current.ClusterId,
                MaxCpuUtilization = current.MaxCpuUtilization,
                MaxActiveCalls = current.MaxActiveCalls,
                UseExternalIp = current.UseExternalIp,
                LocalNet = current.LocalNet,
                Nat1To1Ip = current.Nat1To1Ip,
                MediaUseExternalIp = current.MediaUseExternalIp,
                MediaNat1To1Ip = current.MediaNat1To1Ip,
                UdpMaxPayload = current.UdpMaxPayload,
                EnableJitterBuffer = current.EnableJitterBuffer,
                EnableJitterBufferProbability = current.EnableJitterBufferProbability,
                Logging = new LiveKitServiceLoggingConfiguration { Level = current.Logging.Level },
            };
        configure(configuration);
        _ = configuration.ToYaml(sip.Resource.JsonLoggingEnabled);
        sip.Resource.Configuration = configuration;
        return sip;
    }

    /// <summary>
    /// Appends additional raw YAML configuration to the LiveKit SIP service configuration.
    /// </summary>
    /// <param name="sip">The SIP resource builder.</param>
    /// <param name="yaml">A valid YAML string containing additional top-level settings.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitSipResource}"/> with additional configuration applied.</returns>
    [AspireExport]
    public static IResourceBuilder<LiveKitSipResource> WithSipAdditionalConfiguration(
        this IResourceBuilder<LiveKitSipResource> sip,
        string yaml)
    {
        ArgumentNullException.ThrowIfNull(sip);
        sip.Resource.AdditionalConfiguration = LiveKitConfigurationYaml.ValidateAdditionalConfiguration(
            yaml,
            nameof(yaml),
            "api_key",
            "api_secret",
            "ws_url",
            "redis",
            "sip_port",
            "rtp_port",
            "health_port",
            "prometheus_port",
            "tls",
            "logging",
            "cluster_id",
            "max_cpu_utilization",
            "max_active_calls",
            "use_external_ip",
            "local_net",
            "nat_1_to_1_ip",
            "media_use_external_ip",
            "media_nat_1_to_1_ip",
            "udp_max_payload",
            "enable_jitter_buffer",
            "enable_jitter_buffer_prob");
        return sip;
    }

    
    /// <summary>Enables JSON console logging for a LiveKit SIP resource.</summary>
    /// <param name="resourceBuilder">The SIP resource.</param>
    /// <returns>The configured resource.</returns>
    [AspireExport]
    internal static IResourceBuilder<LiveKitSipResource> WithSipJsonLogging(
        IResourceBuilder<LiveKitSipResource> resourceBuilder) =>
        LiveKitServiceLoggingExtensions.WithJsonLogging(resourceBuilder);

    /// <summary>Enables OTLP log forwarding for a LiveKit SIP resource.</summary>
    /// <param name="resourceBuilder">The SIP resource.</param>
    /// <returns>The configured resource.</returns>
    [AspireExport]
    internal static IResourceBuilder<LiveKitSipResource> WithSipStructuredLogs(
        IResourceBuilder<LiveKitSipResource> resourceBuilder) =>
        LiveKitServiceLoggingExtensions.WithStructuredLogs(resourceBuilder);

    private static ReferenceExpression CreateSipConfiguration<TResource>(
        LiveKitSipResource resource,
        IResourceBuilder<TResource> redis)
        where TResource : IResource, IResourceWithConnectionString
    {
        var healthPort = LiveKitHostingHelpers.GetHttpPortConfiguration(resource.HealthPort, "health_port");
        var prometheusPort = LiveKitHostingHelpers.GetHttpPortConfiguration(resource.PrometheusPort, "prometheus_port");
        var tls = resource.TlsPort is { } tlsPort
            ? ReferenceExpression.Create(
                $"tls:\n  port: {tlsPort.ToString(CultureInfo.InvariantCulture)}\n" +
                $"  cert_file: {LiveKitConfigurationYaml.SerializeScalar(resource.TlsCertificateFile!)}\n" +
                $"  key_file: {LiveKitConfigurationYaml.SerializeScalar(resource.TlsKeyFile!)}\n")
            : ReferenceExpression.Empty;
        return ReferenceExpression.Create(
            $"{LiveKitRedisConfiguration.Create(redis)}" +
            $"sip_port: {resource.SipPort.ToString(CultureInfo.InvariantCulture)}\n" +
            $"rtp_port: {LiveKitConfigurationYaml.SerializeScalar($"{resource.RtpPortStart.ToString(CultureInfo.InvariantCulture)}-{resource.RtpPortEnd.ToString(CultureInfo.InvariantCulture)}")}\n" +
            $"{healthPort}{prometheusPort}{tls}" +
            $"{resource.Configuration?.ToYaml(resource.JsonLoggingEnabled) ?? LiveKitHostingHelpers.GetJsonLoggingConfiguration(resource)}" +
            $"{resource.AdditionalConfiguration ?? string.Empty}");
    }

    private static void ValidateSipCompanionPort(
        int? port,
        int sipPort,
        int rtpPortStart,
        int rtpPortEnd,
        string parameterName)
    {
        if (port == sipPort || (port is not null && port >= rtpPortStart && port <= rtpPortEnd))
        {
            throw new ArgumentException("Health and Prometheus ports must not overlap SIP or RTP listener ports.", parameterName);
        }
    }
}
