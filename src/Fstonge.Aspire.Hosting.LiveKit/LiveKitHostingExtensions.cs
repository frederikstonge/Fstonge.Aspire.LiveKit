using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Sockets;

namespace Fstonge.Aspire.Hosting.LiveKit;

public static partial class LiveKitHostingExtensions
{
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

    public static IResourceBuilder<TResource> WithJsonLogging<TResource>(
        this IResourceBuilder<TResource> resourceBuilder)
        where TResource : LiveKitContainerResource
    {
        ArgumentNullException.ThrowIfNull(resourceBuilder);

        resourceBuilder.Resource.JsonLoggingEnabled = true;
        return resourceBuilder;
    }

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
            .WithImage("livekit/ingress")
            .WithImageTag(version ?? "latest")
            .WithEndpoint(port: rtmpPort, targetPort: rtmpPort, name: "rtmp", protocol: ProtocolType.Tcp)
            .WithHttpEndpoint(port: whipPort, targetPort: whipPort, name: "whip")
            .WithEndpoint(port: whipUdpPort, targetPort: whipUdpPort, name: "whip-udp", protocol: ProtocolType.Udp)
            .WithEnvironment(
                "INGRESS_CONFIG_BODY",
                ReferenceExpression.Create(
                    $"api_key: \"{livekit.Resource.ApiKey}\"\napi_secret: \"{livekit.Resource.ApiSecret}\"\nws_url: \"ws://{livekit.Resource.Name}:7880\"\n{LiveKitRedisConfiguration.Create(redis)}rtmp_port: {rtmpPort.ToString(CultureInfo.InvariantCulture)}\nwhip_port: {whipPort.ToString(CultureInfo.InvariantCulture)}\nrtc_config:\n  udp_port: {whipUdpPort.ToString(CultureInfo.InvariantCulture)}\n{GetJsonLoggingConfiguration(ingressResource)}"));

        ingress.WithReference(livekit.GetEndpoint("http"));
        ingress
            .WaitFor(livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(livekit.Resource))
            .WaitFor(livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(redis.Resource));
        livekit.Resource.RtmpBaseUrl = rtmpBaseUrl;
        livekit.Resource.WhipBaseUrl = whipBaseUrl;

        return ingress;
    }

    private static ReferenceExpression CreateServerConfiguration(LiveKitServerResource resource)
    {
        var configuration = new ReferenceExpressionBuilder();
        if (resource.IsDevelopment)
        {
            configuration.Append($"development: true\n");
        }

        configuration.Append(
            $"port: 7880\nrtc:\n  tcp_port: {resource.RtcTcpPort.ToString(CultureInfo.InvariantCulture)}\n  udp_port: {resource.RtcUdpPort.ToString(CultureInfo.InvariantCulture)}\n");

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

        if (resource.JsonLoggingEnabled)
        {
            configuration.Append($"logging:\n  json: true\n");
        }

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
