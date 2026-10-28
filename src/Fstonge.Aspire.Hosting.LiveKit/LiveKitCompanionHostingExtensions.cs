using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Globalization;
using System.Net.Sockets;

namespace Fstonge.Aspire.Hosting.LiveKit;

public static partial class LiveKitHostingExtensions
{
    /// <summary>
    /// Adds a LiveKit Egress service container to the distributed application.
    /// </summary>
    /// <typeparam name="TResource">The type of the Redis resource with a connection string.</typeparam>
    /// <param name="livekit">The parent LiveKit server resource builder.</param>
    /// <param name="name">The name of the Egress resource.</param>
    /// <param name="redis">The Redis resource used for coordination.</param>
    /// <param name="version">The container image tag version. Defaults to <c>"latest"</c>.</param>
    /// <param name="healthPort">The optional host port for health check endpoints.</param>
    /// <param name="prometheusPort">The optional host port for Prometheus metrics.</param>
    /// <returns>An <see cref="IResourceBuilder{LiveKitEgressResource}"/> for configuring the Egress service.</returns>
    [AspireExportIgnore(Reason = "Use the Redis-specific exported AddEgress overload in generated AppHosts.")]
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

    /// <summary>Adds a LiveKit Egress service using an Aspire Redis resource.</summary>
    /// <param name="livekit">The LiveKit server resource.</param>
    /// <param name="name">The name of the Egress resource.</param>
    /// <param name="redis">The Redis resource used for coordination.</param>
    /// <param name="version">The container image tag version.</param>
    /// <param name="healthPort">The optional health endpoint port.</param>
    /// <param name="prometheusPort">The optional Prometheus metrics port.</param>
    /// <returns>The added Egress resource.</returns>
    [AspireExport("addEgress")]
    internal static IResourceBuilder<LiveKitEgressResource> AddEgressForExport(
        IResourceBuilder<LiveKitServerResource> livekit,
        [ResourceName] string name,
        IResourceBuilder<RedisResource> redis,
        string? version = null,
        int? healthPort = null,
        int? prometheusPort = null) =>
        AddEgress<RedisResource>(livekit, name, redis, version, healthPort, prometheusPort);

    /// <summary>
    /// Configures a bind mount directory for LiveKit Egress local file recording output.
    /// </summary>
    /// <param name="egress">The Egress resource builder.</param>
    /// <param name="hostPath">The directory path on the host machine.</param>
    /// <param name="containerPath">The destination directory inside the container. Defaults to <c>"/out"</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitEgressResource}"/> with local storage configured.</returns>
    [AspireExport]
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

    /// <summary>
    /// Configures Amazon S3 (or S3-compatible) storage for LiveKit Egress recordings and snapshots.
    /// </summary>
    /// <param name="egress">The Egress resource builder.</param>
    /// <param name="bucket">The S3 bucket name.</param>
    /// <param name="region">The AWS region name.</param>
    /// <param name="accessKey">An optional parameter resource for the AWS access key ID.</param>
    /// <param name="secret">An optional parameter resource for the AWS secret access key.</param>
    /// <param name="endpoint">An optional custom S3 endpoint URL (e.g., for MinIO or custom S3-compatible storage).</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitEgressResource}"/> with S3 storage configured.</returns>
    [AspireExport]
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

    /// <summary>
    /// Configures Azure Blob Storage for LiveKit Egress recordings and snapshots.
    /// </summary>
    /// <param name="egress">The Egress resource builder.</param>
    /// <param name="containerName">The Azure Blob Storage container name.</param>
    /// <param name="accountName">The parameter resource containing the Azure storage account name.</param>
    /// <param name="accountKey">The parameter resource containing the Azure storage account key.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitEgressResource}"/> with Azure Blob Storage configured.</returns>
    [AspireExport]
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

    /// <summary>
    /// Appends additional raw YAML configuration to the LiveKit Egress service configuration.
    /// </summary>
    /// <param name="egress">The Egress resource builder.</param>
    /// <param name="yaml">A valid YAML string containing additional top-level settings.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitEgressResource}"/> with additional configuration applied.</returns>
    [AspireExport]
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

    /// <summary>
    /// Adds a LiveKit SIP service container to the distributed application.
    /// </summary>
    /// <typeparam name="TResource">The type of the Redis resource with a connection string.</typeparam>
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
    [AspireExportIgnore(Reason = "Use the Redis-specific exported AddSip overload in generated AppHosts.")]
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

    /// <summary>Adds a LiveKit SIP service using an Aspire Redis resource.</summary>
    /// <param name="livekit">The LiveKit server resource.</param>
    /// <param name="name">The name of the SIP resource.</param>
    /// <param name="redis">The Redis resource used for coordination.</param>
    /// <param name="version">The container image tag version.</param>
    /// <param name="sipPort">The SIP signaling port.</param>
    /// <param name="rtpPortStart">The start of the RTP media port range.</param>
    /// <param name="rtpPortEnd">The end of the RTP media port range.</param>
    /// <param name="healthPort">The optional health endpoint port.</param>
    /// <param name="prometheusPort">The optional Prometheus metrics port.</param>
    /// <returns>The added SIP resource.</returns>
    [AspireExport("addSip")]
    internal static IResourceBuilder<LiveKitSipResource> AddSipForExport(
        IResourceBuilder<LiveKitServerResource> livekit,
        [ResourceName] string name,
        IResourceBuilder<RedisResource> redis,
        string? version = null,
        int sipPort = 5060,
        int rtpPortStart = 10000,
        int rtpPortEnd = 20000,
        int? healthPort = null,
        int? prometheusPort = null) =>
        AddSip<RedisResource>(
            livekit, name, redis, version, sipPort, rtpPortStart, rtpPortEnd, healthPort, prometheusPort);

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
        if (port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Port numbers must be between 1 and 65535.");
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
