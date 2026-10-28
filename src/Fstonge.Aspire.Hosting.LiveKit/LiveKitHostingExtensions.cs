using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Sockets;

namespace Fstonge.Aspire.Hosting.LiveKit;

/// <summary>
/// Provides extension methods for registering and configuring LiveKit resources with .NET Aspire.
/// </summary>
public static partial class LiveKitHostingExtensions
{
    /// <summary>
    /// Adds a LiveKit server container resource to the distributed application builder with auto-generated credentials.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the LiveKit server resource.</param>
    /// <param name="version">The container image tag version. Defaults to <c>"latest"</c>.</param>
    /// <param name="httpPort">The host port for HTTP and WebSocket signaling. Defaults to 7880.</param>
    /// <param name="rtcTcpPort">The host port for RTC TCP fallback connections. Defaults to 7881.</param>
    /// <param name="rtcUdpPort">The host port for primary RTC UDP media. Defaults to 7882.</param>
    /// <returns>An <see cref="IResourceBuilder{LiveKitServerResource}"/> for configuring the LiveKit server.</returns>
    public static IResourceBuilder<LiveKitServerResource> AddLiveKit(
        this IDistributedApplicationBuilder builder,
        string name,
        string? version = null,
        int httpPort = 7880,
        int rtcTcpPort = 7881,
        int rtcUdpPort = 7882)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var apiKey = builder.AddParameter(
            $"{name}-api-key",
            new GenerateParameterDefault { MinLength = 32, Special = false },
            secret: true,
            persist: true);
        var apiSecret = builder.AddParameter(
            $"{name}-api-secret",
            new GenerateParameterDefault { MinLength = 64, Special = false },
            secret: true,
            persist: true);

        return builder.AddLiveKit(name, apiKey, apiSecret, version, httpPort, rtcTcpPort, rtcUdpPort);
    }

    /// <summary>
    /// Adds a LiveKit server container resource to the distributed application builder with explicit API credentials.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the LiveKit server resource.</param>
    /// <param name="apiKey">The parameter resource containing the LiveKit API key.</param>
    /// <param name="apiSecret">The parameter resource containing the LiveKit API secret.</param>
    /// <param name="version">The container image tag version. Defaults to <c>"latest"</c>.</param>
    /// <param name="httpPort">The host port for HTTP and WebSocket signaling. Defaults to 7880.</param>
    /// <param name="rtcTcpPort">The host port for RTC TCP fallback connections. Defaults to 7881.</param>
    /// <param name="rtcUdpPort">The host port for primary RTC UDP media. Defaults to 7882.</param>
    /// <returns>An <see cref="IResourceBuilder{LiveKitServerResource}"/> for configuring the LiveKit server.</returns>
    public static IResourceBuilder<LiveKitServerResource> AddLiveKit(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ParameterResource> apiKey,
        IResourceBuilder<ParameterResource> apiSecret,
        string? version = null,
        int httpPort = 7880,
        int rtcTcpPort = 7881,
        int rtcUdpPort = 7882)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(apiSecret);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(httpPort);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rtcTcpPort);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rtcUdpPort);
        if (httpPort == rtcTcpPort)
        {
            throw new ArgumentException("The HTTP and RTC TCP host ports must be different.", nameof(rtcTcpPort));
        }

        var isDevelopment = builder.Environment.IsDevelopment();
        if (!isDevelopment)
        {
            var description = apiSecret.Resource.Description;
            apiSecret.WithDescription(
                string.IsNullOrWhiteSpace(description)
                    ? "Must contain at least 32 characters."
                    : $"{description}{Environment.NewLine}Must contain at least 32 characters.");
        }

        var resource = new LiveKitServerResource(name, apiKey, apiSecret, rtcTcpPort, rtcUdpPort, isDevelopment);
        var server = builder.AddResource(resource)
            .WithImage("livekit/livekit-server")
            .WithImageTag(version ?? "latest")
            .WithHttpEndpoint(port: httpPort, targetPort: 7880, name: "http")
            .WithEndpoint(port: rtcTcpPort, targetPort: rtcTcpPort, name: "rtc-tcp", protocol: ProtocolType.Tcp)
            .WithEndpoint(port: rtcUdpPort, targetPort: rtcUdpPort, name: "rtc-udp", protocol: ProtocolType.Udp);

        return server.WithEnvironment(
            context => context.EnvironmentVariables["LIVEKIT_CONFIG"] = CreateServerConfiguration(resource));
    }

    /// <summary>
    /// Configures a Redis cache resource for LiveKit clustering and distributed coordination.
    /// </summary>
    /// <typeparam name="TResource">The type of the Redis resource with a connection string.</typeparam>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="redis">The Redis resource builder.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitServerResource}"/> with Redis configured.</returns>
    public static IResourceBuilder<LiveKitServerResource> WithRedis<TResource>(
        this IResourceBuilder<LiveKitServerResource> livekit,
        IResourceBuilder<TResource> redis)
        where TResource : IResource, IResourceWithConnectionString
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentNullException.ThrowIfNull(redis);

        livekit.Resource.RedisConfiguration = LiveKitRedisConfiguration.Create(redis);
        var redisResource = livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(redis.Resource);
        return livekit.WaitFor(redisResource);
    }

    /// <summary>
    /// Appends additional raw YAML configuration to the LiveKit server configuration.
    /// </summary>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="yaml">A valid YAML string containing additional top-level settings.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitServerResource}"/> with additional configuration applied.</returns>
    public static IResourceBuilder<LiveKitServerResource> WithLiveKitAdditionalConfiguration(
        this IResourceBuilder<LiveKitServerResource> livekit,
        string yaml)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        livekit.Resource.AdditionalConfiguration = ValidateAdditionalConfiguration(
            yaml,
            nameof(yaml),
            "development",
            "port",
            "log_level",
            "rtc",
            "redis",
            "turn",
            "ingress",
            "logging",
            "keys");
        return livekit;
    }

    /// <summary>
    /// Configures JSON formatted logging for a LiveKit container resource.
    /// </summary>
    /// <typeparam name="TResource">The type of the LiveKit container resource.</typeparam>
    /// <param name="resourceBuilder">The LiveKit container resource builder.</param>
    /// <returns>The configured resource builder.</returns>
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

    /// <summary>
    /// Enables the built-in TURN server on UDP for NAT traversal.
    /// </summary>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="udpPort">The UDP port for the TURN listener. Defaults to 3478.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitServerResource}"/> with TURN enabled.</returns>
    public static IResourceBuilder<LiveKitServerResource> WithTurn(
        this IResourceBuilder<LiveKitServerResource> livekit,
        int udpPort = 3478)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(udpPort);
        if (udpPort == livekit.Resource.RtcUdpPort)
        {
            throw new ArgumentException("The TURN and RTC UDP host ports must be different.", nameof(udpPort));
        }

        livekit.Resource.TurnUdpPort = udpPort;
        return livekit
            .WithEndpoint(port: udpPort, targetPort: udpPort, name: "turn-udp", protocol: ProtocolType.Udp);
    }

    /// <summary>
    /// Enables TURN over TLS (TURNS) on TCP with specified domain and certificate files.
    /// </summary>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="domain">The public domain name configured for TURN.</param>
    /// <param name="certificateFile">The path to the TLS certificate file.</param>
    /// <param name="keyFile">The path to the TLS private key file.</param>
    /// <param name="tlsPort">The TCP port for TURNS traffic. Defaults to 5349.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitServerResource}"/> with TURNS enabled.</returns>
    public static IResourceBuilder<LiveKitServerResource> WithTurnTls(
        this IResourceBuilder<LiveKitServerResource> livekit,
        string domain,
        string certificateFile,
        string keyFile,
        int tlsPort = 5349)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFile);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tlsPort);
        if (tlsPort == 7880 || tlsPort == livekit.Resource.RtcTcpPort)
        {
            throw new ArgumentException("The TURN TLS and TCP service ports must be different.", nameof(tlsPort));
        }

        livekit.Resource.TurnTlsPort = tlsPort;
        livekit.Resource.TurnDomain = domain;
        livekit.Resource.TurnCertificateFile = certificateFile;
        livekit.Resource.TurnKeyFile = keyFile;

        return livekit
            .WithEndpoint(port: tlsPort, targetPort: tlsPort, name: "turn-tls", protocol: ProtocolType.Tcp);
    }

    /// <summary>
    /// Injects LiveKit connection environment variables (<c>LIVEKIT_URL</c>, and optionally <c>LIVEKIT_API_KEY</c> and <c>LIVEKIT_API_SECRET</c>) into a dependent project resource.
    /// </summary>
    /// <typeparam name="TProject">The dependent resource type supporting environment variables.</typeparam>
    /// <param name="project">The dependent resource builder.</param>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="includeCredentials">A value indicating whether to include API credentials in the environment variables. Defaults to <see langword="false"/>.</param>
    /// <returns>The updated project resource builder.</returns>
    public static IResourceBuilder<TProject> WithLiveKitReference<TProject>(
        this IResourceBuilder<TProject> project,
        IResourceBuilder<LiveKitServerResource> livekit,
        bool includeCredentials = false)
        where TProject : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(livekit);

        project.WithEnvironment("LIVEKIT_URL", livekit.GetEndpoint("http"));

        if (includeCredentials)
        {
            project.WithEnvironment("LIVEKIT_API_KEY", livekit.Resource.ApiKey);
            project.WithEnvironment("LIVEKIT_API_SECRET", livekit.Resource.ApiSecret);
        }

        return project;
    }

    /// <summary>
    /// Adds a LiveKit Ingress service container to the distributed application.
    /// </summary>
    /// <typeparam name="TResource">The type of the Redis resource with a connection string.</typeparam>
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
    public static IResourceBuilder<LiveKitIngressResource> AddIngress<TResource>(
        this IResourceBuilder<LiveKitServerResource> livekit,
        string name,
        IResourceBuilder<TResource> redis,
        string rtmpBaseUrl,
        string whipBaseUrl,
        string? version = null,
        int rtmpPort = 1935,
        int whipPort = 8080,
        int whipUdpPort = 7885)
        where TResource : IResource, IResourceWithConnectionString
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
    /// Appends additional raw YAML configuration to the LiveKit Ingress service configuration.
    /// </summary>
    /// <param name="ingress">The Ingress resource builder.</param>
    /// <param name="yaml">A valid YAML string containing additional top-level settings.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitIngressResource}"/> with additional configuration applied.</returns>
    public static IResourceBuilder<LiveKitIngressResource> WithIngressAdditionalConfiguration(
        this IResourceBuilder<LiveKitIngressResource> ingress,
        string yaml)
    {
        ArgumentNullException.ThrowIfNull(ingress);
        ingress.Resource.AdditionalConfiguration = ValidateAdditionalConfiguration(
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
            $"{resource.Configuration?.ToYaml(resource.JsonLoggingEnabled) ?? GetJsonLoggingConfiguration(resource)}" +
            $"{resource.AdditionalConfiguration ?? string.Empty}");

    private static ReferenceExpression CreateServerConfiguration(LiveKitServerResource resource)
    {
        var configuration = new ReferenceExpressionBuilder();
        if (resource.IsDevelopment)
        {
            configuration.Append($"development: true\n");
        }

        var rtcConfiguration = new Dictionary<string, object?>
        {
            ["tcp_port"] = resource.RtcTcpPort,
            ["udp_port"] = resource.RtcUdpPort
        };
        if (resource.Configuration is { } serverConfiguration)
        {
            foreach (var (key, value) in serverConfiguration.Rtc.ToDictionary())
            {
                rtcConfiguration[key] = value;
            }
        }

        configuration.Append($"port: 7880\n");
        configuration.Append(
            $"{LiveKitConfigurationYaml.Serialize(new Dictionary<string, object?>
            {
                ["rtc"] = rtcConfiguration
            })}");

        if (resource.RedisConfiguration is { } redisSection)
        {
            configuration.Append($"{redisSection}");
        }

        var turnSection = ReferenceExpression.Empty;
        if (resource.TurnUdpPort is not null || resource.TurnTlsPort is not null)
        {
            var udpSetting = resource.TurnUdpPort is { } udpPort
                ? ReferenceExpression.Create($"  udp_port: {udpPort.ToString(CultureInfo.InvariantCulture)}\n")
                : ReferenceExpression.Empty;
            var tlsSettings = resource.TurnTlsPort is { } tlsPort
                && resource.TurnDomain is { } turnDomain
                && resource.TurnCertificateFile is { } certificateFile
                && resource.TurnKeyFile is { } keyFile
                    ? ReferenceExpression.Create(
                        $"  tls_port: {tlsPort.ToString(CultureInfo.InvariantCulture)}\n  domain: {QuoteYamlString(turnDomain)}\n  cert_file: {QuoteYamlString(certificateFile)}\n  key_file: {QuoteYamlString(keyFile)}\n")
                    : ReferenceExpression.Empty;
            turnSection = ReferenceExpression.Create($"turn:\n  enabled: true\n{udpSetting}{tlsSettings}");
        }

        configuration.Append($"{turnSection}");

        var ingressSection = resource.RtmpBaseUrl is not null && resource.WhipBaseUrl is not null
            ? ReferenceExpression.Create(
                $"ingress:\n  rtmp_base_url: {QuoteYamlString(resource.RtmpBaseUrl)}\n  whip_base_url: {QuoteYamlString(resource.WhipBaseUrl)}\n")
            : ReferenceExpression.Empty;
        configuration.Append($"{ingressSection}");

        if (resource.Configuration is { } configurationOptions)
        {
            configuration.Append($"{LiveKitConfigurationYaml.Serialize(configurationOptions.ToDictionary())}");
        }

        if (LiveKitConfigurationYaml.CreateLogging(null, resource.JsonLoggingEnabled) is { } loggingConfiguration)
        {
            configuration.Append($"{LiveKitConfigurationYaml.Serialize(new Dictionary<string, object?>
            {
                ["logging"] = loggingConfiguration
            })}");
        }

        configuration.Append($"{resource.AdditionalConfiguration ?? string.Empty}");
        configuration.Append($"keys:\n  \"{resource.ApiKey}\": \"{resource.ApiSecret}\"\n");

        return configuration.Build();
    }

    private static string GetJsonLoggingConfiguration(LiveKitContainerResource resource) =>
        resource.JsonLoggingEnabled ? "logging:\n  json: true\n" : string.Empty;

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
