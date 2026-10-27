using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Globalization;
using System.Net.Sockets;

namespace Fstonge.Aspire.Hosting.LiveKit;

public static partial class LiveKitHostingExtensions
{
    public static IResourceBuilder<LiveKitEgressResource> AddEgress<TResource>(
        this IResourceBuilder<LiveKitServerResource> livekit,
        string name,
        IResourceBuilder<TResource> redis,
        string? version = null,
        int? healthPort = null,
        int? prometheusPort = null)
        where TResource : IResource, IResourceWithConnectionString
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(redis);
        ValidateOptionalPort(healthPort, nameof(healthPort));
        ValidateOptionalPort(prometheusPort, nameof(prometheusPort));
        if (healthPort is not null && healthPort == prometheusPort)
        {
            throw new ArgumentException("The health and Prometheus ports must be different.", nameof(prometheusPort));
        }

        livekit.WithRedis(redis);

        var resource = new LiveKitEgressResource(name, livekit.Resource, healthPort, prometheusPort);
        var egress = livekit.ApplicationBuilder
            .AddResource(resource)
            .WithParentRelationship(livekit.Resource)
            .WithImage("livekit/egress")
            .WithImageTag(version ?? "latest");

        if (healthPort is { } egressHealthPort)
        {
            egress = egress
                .WithHttpEndpoint(
                    port: egressHealthPort,
                    targetPort: egressHealthPort,
                    name: "health");
        }

        if (prometheusPort is { } egressPrometheusPort)
        {
            egress = egress
                .WithHttpEndpoint(
                    port: egressPrometheusPort,
                    targetPort: egressPrometheusPort,
                    name: "prometheus");
        }

        egress
            .WithEnvironment("LIVEKIT_API_KEY", livekit.Resource.ApiKey)
            .WithEnvironment("LIVEKIT_API_SECRET", livekit.Resource.ApiSecret)
            .WithEnvironment("LIVEKIT_WS_URL", $"ws://{livekit.Resource.Name}:7880")
            .WithEnvironment(context =>
                context.EnvironmentVariables["EGRESS_CONFIG_BODY"] = CreateEgressConfiguration(resource, redis));

        egress
            .WaitFor(livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(livekit.Resource))
            .WaitFor(livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(redis.Resource));

        return egress;
    }

    public static IResourceBuilder<LiveKitEgressResource> WithEgressLocalOutputDirectory(
        this IResourceBuilder<LiveKitEgressResource> egress,
        string hostPath,
        string containerPath = "/out")
    {
        ArgumentNullException.ThrowIfNull(egress);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerPath);
        if (!containerPath.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("The container output path must be an absolute Linux path.", nameof(containerPath));
        }

        if (egress.Resource.LocalOutputContainerPath is not null)
        {
            throw new InvalidOperationException("An Egress output directory has already been configured.");
        }

        egress.Resource.LocalOutputContainerPath = containerPath;
        return egress.WithBindMount(hostPath, containerPath);
    }

    public static IResourceBuilder<LiveKitEgressResource> WithEgressS3Storage(
        this IResourceBuilder<LiveKitEgressResource> egress,
        string bucket,
        string region,
        IResourceBuilder<ParameterResource>? accessKey = null,
        IResourceBuilder<ParameterResource>? secret = null,
        string? endpoint = null)
    {
        ArgumentNullException.ThrowIfNull(egress);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        if ((accessKey is null) != (secret is null))
        {
            throw new ArgumentException("Both S3 access key and secret parameters must be supplied.");
        }

        EnsureNoEgressStorage(egress.Resource);
        var configuration =
            "storage:\n" +
            "  s3:\n" +
            $"    bucket: {QuoteYamlString(bucket)}\n" +
            $"    region: {QuoteYamlString(region)}\n" +
            (endpoint is null ? string.Empty : $"    endpoint: {QuoteYamlString(endpoint)}\n");
        egress.Resource.StorageConfiguration = configuration;

        if (accessKey is not null && secret is not null)
        {
            egress.WithEnvironment("AWS_ACCESS_KEY_ID", accessKey);
            egress.WithEnvironment("AWS_SECRET_ACCESS_KEY", secret);
        }

        return egress;
    }

    public static IResourceBuilder<LiveKitEgressResource> WithEgressAzureStorage(
        this IResourceBuilder<LiveKitEgressResource> egress,
        string containerName,
        IResourceBuilder<ParameterResource> accountName,
        IResourceBuilder<ParameterResource> accountKey)
    {
        ArgumentNullException.ThrowIfNull(egress);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentNullException.ThrowIfNull(accountName);
        ArgumentNullException.ThrowIfNull(accountKey);
        EnsureNoEgressStorage(egress.Resource);

        egress.Resource.StorageConfiguration =
            "storage:\n" +
            "  azure:\n" +
            $"    container_name: {QuoteYamlString(containerName)}\n";
        egress
            .WithEnvironment("AZURE_STORAGE_ACCOUNT", accountName)
            .WithEnvironment("AZURE_STORAGE_KEY", accountKey);

        return egress;
    }

    public static IResourceBuilder<LiveKitEgressResource> WithEgressAdditionalConfiguration(
        this IResourceBuilder<LiveKitEgressResource> egress,
        string yaml)
    {
        ArgumentNullException.ThrowIfNull(egress);
        egress.Resource.AdditionalConfiguration = ValidateAdditionalConfiguration(
            yaml,
            nameof(yaml),
            "api_key",
            "api_secret",
            "ws_url",
            "redis",
            "health_port",
            "prometheus_port",
            "storage",
            "logging",
            "template_port",
            "debug_handler_port",
            "pulse_sink_reap_grace_sec",
            "template_base",
            "enable_chrome_sandbox");
        return egress;
    }

    public static IResourceBuilder<LiveKitSipResource> AddSip<TResource>(
        this IResourceBuilder<LiveKitServerResource> livekit,
        string name,
        IResourceBuilder<TResource> redis,
        string? version = null,
        int sipPort = 5060,
        int rtpPortStart = 10000,
        int rtpPortEnd = 20000,
        int? healthPort = null,
        int? prometheusPort = null)
        where TResource : IResource, IResourceWithConnectionString
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

        ValidateOptionalPort(healthPort, nameof(healthPort));
        ValidateOptionalPort(prometheusPort, nameof(prometheusPort));
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

    public static IResourceBuilder<LiveKitSipResource> WithSipAdditionalConfiguration(
        this IResourceBuilder<LiveKitSipResource> sip,
        string yaml)
    {
        ArgumentNullException.ThrowIfNull(sip);
        sip.Resource.AdditionalConfiguration = ValidateAdditionalConfiguration(
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

    private static ReferenceExpression CreateEgressConfiguration<TResource>(
        LiveKitEgressResource resource,
        IResourceBuilder<TResource> redis)
        where TResource : IResource, IResourceWithConnectionString
    {
        var healthPort = GetHttpPortConfiguration(resource.HealthPort, "health_port");
        var prometheusPort = GetHttpPortConfiguration(resource.PrometheusPort, "prometheus_port");
        return ReferenceExpression.Create(
            $"{LiveKitRedisConfiguration.Create(redis)}" +
            $"{healthPort}{prometheusPort}{resource.StorageConfiguration ?? string.Empty}" +
            $"{resource.Configuration?.ToYaml(resource.JsonLoggingEnabled) ?? GetJsonLoggingConfiguration(resource)}" +
            $"{resource.AdditionalConfiguration ?? string.Empty}");
    }

    private static ReferenceExpression CreateSipConfiguration<TResource>(
        LiveKitSipResource resource,
        IResourceBuilder<TResource> redis)
        where TResource : IResource, IResourceWithConnectionString
    {
        var healthPort = GetHttpPortConfiguration(resource.HealthPort, "health_port");
        var prometheusPort = GetHttpPortConfiguration(resource.PrometheusPort, "prometheus_port");
        var tls = resource.TlsPort is { } tlsPort
            ? ReferenceExpression.Create(
                $"tls:\n  port: {tlsPort.ToString(CultureInfo.InvariantCulture)}\n" +
                $"  cert_file: {QuoteYamlString(resource.TlsCertificateFile!)}\n" +
                $"  key_file: {QuoteYamlString(resource.TlsKeyFile!)}\n")
            : ReferenceExpression.Empty;
        return ReferenceExpression.Create(
            $"{LiveKitRedisConfiguration.Create(redis)}" +
            $"sip_port: {resource.SipPort.ToString(CultureInfo.InvariantCulture)}\n" +
            $"rtp_port: {QuoteYamlString($"{resource.RtpPortStart.ToString(CultureInfo.InvariantCulture)}-{resource.RtpPortEnd.ToString(CultureInfo.InvariantCulture)}")}\n" +
            $"{healthPort}{prometheusPort}{tls}" +
            $"{resource.Configuration?.ToYaml(resource.JsonLoggingEnabled) ?? GetJsonLoggingConfiguration(resource)}" +
            $"{resource.AdditionalConfiguration ?? string.Empty}");
    }

    private static ReferenceExpression GetHttpPortConfiguration(int? port, string key) =>
        port is null
            ? ReferenceExpression.Empty
            : ReferenceExpression.Create($"{key}: {port.Value.ToString(CultureInfo.InvariantCulture)}\n");

    private static string ValidateAdditionalConfiguration(
        string yaml,
        string parameterName,
        params string[] reservedKeys) =>
        LiveKitConfigurationYaml.ValidateAdditionalConfiguration(yaml, parameterName, reservedKeys);

    private static string QuoteYamlString(string value) =>
        LiveKitConfigurationYaml.SerializeScalar(value);

    private static void EnsureNoEgressStorage(LiveKitEgressResource resource)
    {
        if (resource.StorageConfiguration is not null)
        {
            throw new InvalidOperationException("Only one Egress storage provider can be configured.");
        }
    }

    private static void ValidateOptionalPort(int? port, string parameterName)
    {
        if (port is <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Port numbers must be positive.");
        }
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
