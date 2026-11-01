using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

public static class LiveKitEgressHostingExtensions
{
    /// <summary>
    /// Adds a LiveKit Egress service container to the distributed application.
    /// </summary>
    /// <param name="livekit">The parent LiveKit server resource builder.</param>
    /// <param name="name">The name of the Egress resource.</param>
    /// <param name="redis">The Redis resource used for coordination.</param>
    /// <param name="version">The container image tag version. Defaults to <c>"latest"</c>.</param>
    /// <param name="healthPort">The optional host port for health check endpoints.</param>
    /// <param name="prometheusPort">The optional host port for Prometheus metrics.</param>
    /// <returns>An <see cref="IResourceBuilder{LiveKitEgressResource}"/> for configuring the Egress service.</returns>
    [AspireExport]
    public static IResourceBuilder<LiveKitEgressResource> AddEgress(
        this IResourceBuilder<LiveKitServerResource> livekit,
        string name,
        IResourceBuilder<RedisResource> redis,
        string? version = null,
        int? healthPort = null,
        int? prometheusPort = null)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(redis);
        LiveKitHostingHelpers.ValidateOptionalPort(healthPort, nameof(healthPort));
        LiveKitHostingHelpers.ValidateOptionalPort(prometheusPort, nameof(prometheusPort));
        if (healthPort is not null && healthPort == prometheusPort)
        {
            throw new ArgumentException("The health and Prometheus ports must be different.", nameof(prometheusPort));
        }

        livekit.WithLiveKitServerRedis(redis);

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
            $"    bucket: {LiveKitConfigurationYaml.SerializeScalar(bucket)}\n" +
            $"    region: {LiveKitConfigurationYaml.SerializeScalar(region)}\n" +
            (endpoint is null ? string.Empty : $"    endpoint: {LiveKitConfigurationYaml.SerializeScalar(endpoint)}\n");
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
            $"    container_name: {LiveKitConfigurationYaml.SerializeScalar(containerName)}\n";
        egress
            .WithEnvironment("AZURE_STORAGE_ACCOUNT", accountName)
            .WithEnvironment("AZURE_STORAGE_KEY", accountKey);

        return egress;
    }

    /// <summary>
    /// Applies typed configuration settings to the LiveKit Egress service.
    /// </summary>
    /// <param name="egress">The LiveKit Egress resource builder.</param>
    /// <param name="configure">A delegate to configure the <see cref="LiveKitEgressConfiguration"/>.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitEgressResource}"/> with the configuration applied.</returns>
    [AspireExportIgnore(Reason = "Use WithEgressAdditionalConfiguration to configure Egress in generated AppHosts.")]
    public static IResourceBuilder<LiveKitEgressResource> WithEgressConfiguration(
        this IResourceBuilder<LiveKitEgressResource> egress,
        Action<LiveKitEgressConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(egress);
        ArgumentNullException.ThrowIfNull(configure);
        var current = egress.Resource.Configuration;
        var configuration = current is null
            ? new LiveKitEgressConfiguration()
            : new LiveKitEgressConfiguration
            {
                TemplatePort = current.TemplatePort,
                DebugHandlerPort = current.DebugHandlerPort,
                PulseSinkReapGraceSeconds = current.PulseSinkReapGraceSeconds,
                TemplateBase = current.TemplateBase,
                EnableChromeSandbox = current.EnableChromeSandbox,
                Logging = new LiveKitServiceLoggingConfiguration { Level = current.Logging.Level },
            };
        configure(configuration);
        _ = configuration.ToYaml(egress.Resource.JsonLoggingEnabled);
        egress.Resource.Configuration = configuration;
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
        egress.Resource.AdditionalConfiguration = LiveKitConfigurationYaml.ValidateAdditionalConfiguration(
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

    /// <summary>Enables JSON console logging for a LiveKit Egress resource.</summary>
    /// <param name="resourceBuilder">The Egress resource.</param>
    /// <returns>The configured resource.</returns>
    [AspireExport]
    internal static IResourceBuilder<LiveKitEgressResource> WithEgressJsonLogging(
        IResourceBuilder<LiveKitEgressResource> resourceBuilder) =>
        LiveKitServiceLoggingExtensions.WithJsonLogging(resourceBuilder);

    /// <summary>Enables OTLP log forwarding for a LiveKit Egress resource.</summary>
    /// <param name="resourceBuilder">The Egress resource.</param>
    /// <returns>The configured resource.</returns>
    [AspireExport]
    internal static IResourceBuilder<LiveKitEgressResource> WithEgressStructuredLogs(
        IResourceBuilder<LiveKitEgressResource> resourceBuilder) =>
        LiveKitServiceLoggingExtensions.WithStructuredLogs(resourceBuilder);

    private static ReferenceExpression CreateEgressConfiguration<TResource>(
        LiveKitEgressResource resource,
        IResourceBuilder<TResource> redis)
        where TResource : IResource, IResourceWithConnectionString
    {
        var healthPort = LiveKitHostingHelpers.GetHttpPortConfiguration(resource.HealthPort, "health_port");
        var prometheusPort = LiveKitHostingHelpers.GetHttpPortConfiguration(resource.PrometheusPort, "prometheus_port");
        return ReferenceExpression.Create(
            $"{LiveKitRedisConfiguration.Create(redis)}" +
            $"{healthPort}{prometheusPort}{resource.StorageConfiguration ?? string.Empty}" +
            $"{resource.Configuration?.ToYaml(resource.JsonLoggingEnabled) ?? LiveKitHostingHelpers.GetJsonLoggingConfiguration(resource)}" +
            $"{resource.AdditionalConfiguration ?? string.Empty}");
    }

    

    private static void EnsureNoEgressStorage(LiveKitEgressResource resource)
    {
        if (resource.StorageConfiguration is not null)
        {
            throw new InvalidOperationException("Only one Egress storage provider can be configured.");
        }
    }
}
