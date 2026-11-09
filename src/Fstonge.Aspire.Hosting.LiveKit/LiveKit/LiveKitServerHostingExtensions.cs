using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LiveKit.Configuration.Server;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Net.Sockets;
using YamlDotNet.RepresentationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for registering and configuring LiveKit server resources with .NET Aspire.
/// </summary>
public static class LiveKitServerHostingExtensions
{
    /// <summary>
    /// Adds a reference to a LiveKit server managed outside the Aspire application, using an Aspire parameter for its URL.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the external LiveKit resource.</param>
    /// <param name="url">The parameter resource containing the absolute HTTP or HTTPS API URL.</param>
    /// <param name="apiKey">An optional parameter resource containing the LiveKit API key.</param>
    /// <param name="apiSecret">An optional parameter resource containing the LiveKit API secret.</param>
    /// <returns>An <see cref="IResourceBuilder{ExternalLiveKitServerResource}"/> for referencing the external server.</returns>
    [AspireExport]
    public static IResourceBuilder<ExternalLiveKitServerResource> AddExternalLiveKitServer(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> url,
        IResourceBuilder<ParameterResource>? apiKey = null,
        IResourceBuilder<ParameterResource>? apiSecret = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(url);

        if ((apiKey is null) != (apiSecret is null))
        {
            throw new ArgumentException("Both LiveKit API key and secret parameters must be supplied.");
        }

        var resource = new ExternalLiveKitServerResource(
            name,
            ReferenceExpression.Create($"{url}"),
            apiKey,
            apiSecret);
        return builder.AddResource(resource);
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
    [AspireExport]
    public static IResourceBuilder<LiveKitServerResource> AddLiveKitServer(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? apiKey = null,
        IResourceBuilder<ParameterResource>? apiSecret = null,
        string? version = null,
        int httpPort = 7880,
        int rtcTcpPort = 7881,
        int rtcUdpPort = 7882)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(httpPort);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rtcTcpPort);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rtcUdpPort);
        if (httpPort == rtcTcpPort)
        {
            throw new ArgumentException("The HTTP and RTC TCP host ports must be different.", nameof(rtcTcpPort));
        }

        apiKey ??= builder.AddParameter(
            $"{name}-api-key",
            new GenerateParameterDefault { MinLength = 32, Special = false },
            secret: true,
            persist: true);
        apiSecret ??= builder.AddParameter(
            $"{name}-api-secret",
            new GenerateParameterDefault { MinLength = 64, Special = false },
            secret: true,
            persist: true);

        var description = apiSecret.Resource.Description;
        apiSecret.WithDescription(
            string.IsNullOrWhiteSpace(description)
                ? "Must contain at least 32 characters."
                : $"{description}{Environment.NewLine}Must contain at least 32 characters.");

        var isDevelopment = builder.Environment.IsDevelopment();

        var resource = new LiveKitServerResource(name, apiKey, apiSecret, rtcTcpPort, rtcUdpPort, isDevelopment);
        var server = builder.AddResource(resource)
            .WithImage("livekit/livekit-server")
            .WithImageTag(version ?? "latest")
            .WithHttpEndpoint(port: httpPort, targetPort: 7880, name: "http")
            .WithEndpoint(port: rtcTcpPort, targetPort: rtcTcpPort, name: "rtc-tcp", protocol: ProtocolType.Tcp)
            .WithEndpoint(port: rtcUdpPort, targetPort: rtcUdpPort, name: "rtc-udp", protocol: ProtocolType.Udp);

        resource.SetUrl(ReferenceExpression.Create($"{server.GetEndpoint("http")}"));
        return server.WithEnvironment(
            context => context.EnvironmentVariables["LIVEKIT_CONFIG"] = CreateServerConfiguration(resource));
    }

    /// <summary>
    /// Configures a Redis cache resource for LiveKit clustering and distributed coordination.
    /// </summary>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="redis">The Redis resource builder.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitServerResource}"/> with Redis configured.</returns>
    [AspireExport]
    public static IResourceBuilder<LiveKitServerResource> WithLiveKitServerRedis(
        this IResourceBuilder<LiveKitServerResource> livekit,
        IResourceBuilder<RedisResource> redis)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentNullException.ThrowIfNull(redis);

        livekit.Resource.RedisConfiguration = LiveKitRedisConfiguration.Create(redis);
        var redisResource = livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(redis.Resource);
        return livekit.WaitFor(redisResource);
    }

    /// <summary>
    /// Applies typed configuration settings to the LiveKit server.
    /// </summary>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="configure">A delegate to configure the generated upstream server model.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitServerResource}"/> with the configuration applied.</returns>
    [AspireExportIgnore(Reason = "Use WithLiveKitServerAdditionalConfiguration to configure the server in generated AppHosts.")]
    public static IResourceBuilder<LiveKitServerResource> WithLiveKitServerConfiguration(
        this IResourceBuilder<LiveKitServerResource> livekit,
        Action<LiveKitServerUpstreamConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentNullException.ThrowIfNull(configure);
        var current = livekit.Resource.Configuration;
        var configuration = current is null
            ? new LiveKitServerUpstreamConfiguration()
            : LiveKitConfigurationYaml.CloneConfiguration(current);
        configure(configuration);
        var configurationNode = LiveKitConfigurationYaml.CreateConfigurationNode(
            configuration,
            nameof(configure),
            "development",
            "port",
            "redis",
            "turn",
            "ingress",
            "keys");
        ValidateServerRtcConfiguration(configurationNode, nameof(configure));
        if (configurationNode.Children.Count == 0)
        {
            throw new InvalidOperationException("At least one LiveKit server configuration setting must be provided.");
        }

        livekit.Resource.Configuration = configuration;
        return livekit;
    }

    /// <summary>
    /// Appends additional raw YAML configuration to the LiveKit server configuration.
    /// </summary>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="yaml">A valid YAML string containing additional top-level settings.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitServerResource}"/> with additional configuration applied.</returns>
    [AspireExport]
    public static IResourceBuilder<LiveKitServerResource> WithLiveKitServerAdditionalConfiguration(
        this IResourceBuilder<LiveKitServerResource> livekit,
        string yaml)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        livekit.Resource.AdditionalConfiguration = LiveKitConfigurationYaml.ValidateAdditionalConfiguration(
            yaml,
            nameof(yaml),
            "development",
            "port",
            "rtc",
            "redis",
            "turn",
            "ingress",
            "logging",
            "keys");
        return livekit;
    }

    /// <summary>Enables JSON console logging for the LiveKit server resource.</summary>
    /// <param name="resourceBuilder">The LiveKit server resource.</param>
    /// <returns>The configured resource.</returns>
    [AspireExport]
    internal static IResourceBuilder<LiveKitServerResource> WithLiveKitServerJsonLogging(
        IResourceBuilder<LiveKitServerResource> resourceBuilder) =>
        LiveKitServiceLoggingExtensions.WithJsonLogging(resourceBuilder);
    /// <summary>Enables OTLP log forwarding for the LiveKit server resource.</summary>
    /// <param name="resourceBuilder">The LiveKit server resource.</param>
    /// <returns>The configured resource.</returns>

    [AspireExport]
    internal static IResourceBuilder<LiveKitServerResource> WithLiveKitServerStructuredLogs(
        IResourceBuilder<LiveKitServerResource> resourceBuilder) =>
        LiveKitServiceLoggingExtensions.WithStructuredLogs(resourceBuilder);

    /// <summary>
    /// Enables the built-in TURN server on UDP for NAT traversal.
    /// </summary>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="udpPort">The UDP port for the TURN listener. Defaults to 3478.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitServerResource}"/> with TURN enabled.</returns>
    [AspireExport]
    public static IResourceBuilder<LiveKitServerResource> WithLiveKitServerTurn(
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
    [AspireExport]
    public static IResourceBuilder<LiveKitServerResource> WithLiveKitServerTurnTls(
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
    [AspireExportIgnore(Reason = "The method accepts arbitrary C# resource types; generated AppHosts can configure environment variables directly.")]
    public static IResourceBuilder<TProject> WithLiveKitServerReference<TProject, TLiveKit>(
        this IResourceBuilder<TProject> project,
        IResourceBuilder<TLiveKit> livekit,
        bool includeCredentials = false)
        where TProject : IResource, IResourceWithEnvironment
        where TLiveKit : IResource, ILiveKitServerResource
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(livekit);

        project.WithEnvironment("LIVEKIT_URL", livekit.Resource.Url);
        if (includeCredentials)
        {
            var apiKey = livekit.Resource.ApiKey;
            var apiSecret = livekit.Resource.ApiSecret;
            if (apiKey is null || apiSecret is null)
            {
                throw new InvalidOperationException(
                    "API credentials were requested, but the LiveKit resource does not provide API key and secret parameters.");
            }

            project.WithEnvironment("LIVEKIT_API_KEY", apiKey);
            project.WithEnvironment("LIVEKIT_API_SECRET", apiSecret);
        }

        return project;
    }

    private static ReferenceExpression CreateServerConfiguration(LiveKitServerResource resource)
    {
        var configuration = new ReferenceExpressionBuilder();
        if (resource.IsDevelopment)
        {
            configuration.Append($"development: true\n");
        }

        var rtcConfiguration = new YamlMappingNode
        {
            { new YamlScalarNode("tcp_port"), new YamlScalarNode(resource.RtcTcpPort.ToString(CultureInfo.InvariantCulture)) },
            { new YamlScalarNode("udp_port"), new YamlScalarNode(resource.RtcUdpPort.ToString(CultureInfo.InvariantCulture)) },
        };
        if (resource.Configuration is { } serverConfiguration)
        {
            var configurationNode = LiveKitConfigurationYaml.CreateConfigurationNode(
                serverConfiguration,
                nameof(serverConfiguration),
                "development",
                "port",
                "redis",
                "turn",
                "ingress",
                "keys");
            if (resource.JsonLoggingEnabled)
            {
                LiveKitConfigurationYaml.EnableJsonLogging(configurationNode, nameof(serverConfiguration));
            }

            var rtcKey = new YamlScalarNode("rtc");
            if (configurationNode.Children.Remove(rtcKey, out var customRtcNode))
            {
                if (customRtcNode is not YamlMappingNode customRtc)
                {
                    throw new ArgumentException("The RTC configuration must be a YAML mapping.", nameof(serverConfiguration));
                }

                foreach (var (key, value) in customRtc.Children)
                {
                    if (key is not YamlScalarNode { Value: { } keyName })
                    {
                        throw new ArgumentException("RTC configuration keys must be scalar values.", nameof(serverConfiguration));
                    }

                    if (keyName is "tcp_port" or "udp_port")
                    {
                        throw new ArgumentException(
                            $"The RTC '{keyName}' setting is managed by the LiveKit resource configuration.",
                            nameof(serverConfiguration));
                    }

                    rtcConfiguration.Add(key, value);
                }
            }

            ValidateServerPortRange(rtcConfiguration, nameof(serverConfiguration));
            LiveKitConfigurationYaml.ValidateNoDuplicateTopLevelSettings(
                configurationNode,
                resource.AdditionalConfiguration,
                nameof(serverConfiguration));
            configuration.Append($"{LiveKitConfigurationYaml.Serialize(new YamlMappingNode
            {
                { new YamlScalarNode("rtc"), rtcConfiguration },
            })}");
            if (configurationNode.Children.Count > 0)
            {
                configuration.Append($"{LiveKitConfigurationYaml.Serialize(configurationNode)}");
            }
        }
        else
        {
            configuration.Append($"{LiveKitConfigurationYaml.Serialize(new YamlMappingNode
            {
                { new YamlScalarNode("rtc"), rtcConfiguration },
            })}");
        }

        configuration.Append($"port: 7880\n");

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
                        $"  tls_port: {tlsPort.ToString(CultureInfo.InvariantCulture)}\n  domain: {LiveKitConfigurationYaml.SerializeScalar(turnDomain)}\n  cert_file: {LiveKitConfigurationYaml.SerializeScalar(certificateFile)}\n  key_file: {LiveKitConfigurationYaml.SerializeScalar(keyFile)}\n")
                    : ReferenceExpression.Empty;
            turnSection = ReferenceExpression.Create($"turn:\n  enabled: true\n{udpSetting}{tlsSettings}");
        }

        configuration.Append($"{turnSection}");

        var ingressSection = resource.RtmpBaseUrl is not null && resource.WhipBaseUrl is not null
            ? ReferenceExpression.Create(
                $"ingress:\n  rtmp_base_url: {LiveKitConfigurationYaml.SerializeScalar(resource.RtmpBaseUrl)}\n  whip_base_url: {LiveKitConfigurationYaml.SerializeScalar(resource.WhipBaseUrl)}\n")
            : ReferenceExpression.Empty;
        configuration.Append($"{ingressSection}");

        if (resource.Configuration is null && resource.JsonLoggingEnabled)
        {
            var loggingConfiguration = new YamlMappingNode();
            LiveKitConfigurationYaml.EnableJsonLogging(loggingConfiguration, nameof(resource.JsonLoggingEnabled));
            configuration.Append($"{LiveKitConfigurationYaml.Serialize(loggingConfiguration)}");
        }

        configuration.Append($"{resource.AdditionalConfiguration ?? string.Empty}");
        configuration.Append($"keys:\n  \"{resource.ApiKey}\": \"{resource.ApiSecret}\"\n");

        return configuration.Build();
    }

    private static void ValidateServerRtcConfiguration(YamlMappingNode configuration, string parameterName)
    {
        if (!configuration.Children.TryGetValue(new YamlScalarNode("rtc"), out var rtcNode))
        {
            return;
        }

        if (rtcNode is not YamlMappingNode rtc)
        {
            throw new ArgumentException("The RTC configuration must be a YAML mapping.", parameterName);
        }

        foreach (var reservedPort in new[] { "tcp_port", "udp_port" })
        {
            if (rtc.Children.ContainsKey(new YamlScalarNode(reservedPort)))
            {
                throw new ArgumentException(
                    $"The RTC '{reservedPort}' setting is managed by the LiveKit resource configuration.",
                    parameterName);
            }
        }

        ValidateServerPortRange(rtc, parameterName);
    }

    private static void ValidateServerPortRange(YamlMappingNode rtc, string parameterName)
    {
        var startKey = new YamlScalarNode("port_range_start");
        var endKey = new YamlScalarNode("port_range_end");
        var hasStart = rtc.Children.TryGetValue(startKey, out var startNode);
        var hasEnd = rtc.Children.TryGetValue(endKey, out var endNode);
        if (hasStart != hasEnd)
        {
            throw new InvalidOperationException("Both RTC port range bounds must be configured together.");
        }

        if (!hasStart)
        {
            return;
        }

        if (startNode is not YamlScalarNode { Value: { } startText }
            || !int.TryParse(startText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start))
        {
            throw new ArgumentOutOfRangeException(parameterName, "RTC port range start must be an integer.");
        }
        if (endNode is not YamlScalarNode { Value: { } endText }
            || !int.TryParse(endText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
        {
            throw new ArgumentOutOfRangeException(parameterName, "RTC port range end must be an integer.");
        }
        if (start is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(parameterName, "RTC port numbers must be between 1 and 65535.");
        }
        if (end < start || end > 65535)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The RTC port range end must be between its start and 65535.");
        }
    }
}
