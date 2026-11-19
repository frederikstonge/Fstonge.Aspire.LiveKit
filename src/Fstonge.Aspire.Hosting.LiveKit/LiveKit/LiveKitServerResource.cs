using Aspire.Hosting.LiveKit.Configuration.Server;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a LiveKit server container resource in a .NET Aspire distributed application.
/// </summary>
[AspireExport]
public sealed class LiveKitServerResource : LiveKitContainerResource, ILiveKitServerResource
{
    private ReferenceExpression? _url;
    private ReferenceExpression? _webSocketUrl;

    internal LiveKitServerResource(
        string name,
        IResourceBuilder<ParameterResource> apiKey,
        IResourceBuilder<ParameterResource> apiSecret,
        int rtcTcpPort,
        int rtcUdpPort,
        bool isDevelopment)
        : base(name)
    {
        ApiKey = apiKey;
        ApiSecret = apiSecret;
        RtcTcpPort = rtcTcpPort;
        RtcUdpPort = rtcUdpPort;
        IsDevelopment = isDevelopment;
        JsonLoggingEnabled = true;
    }

    internal IResourceBuilder<ParameterResource> ApiKey { get; }

    internal IResourceBuilder<ParameterResource> ApiSecret { get; }

    internal ReferenceExpression Url =>
        _url ?? throw new InvalidOperationException("The LiveKit server URL has not been configured.");

    internal ReferenceExpression WebSocketUrl =>
        _webSocketUrl ?? throw new InvalidOperationException("The LiveKit server WebSocket URL has not been configured.");

    internal void SetUrl(ReferenceExpression url) => _url = url;

    internal void SetWebSocketUrl(ReferenceExpression webSocketUrl) => _webSocketUrl = webSocketUrl;

    ReferenceExpression ILiveKitServerResource.Url => Url;

    ReferenceExpression ILiveKitServerResource.WebSocketUrl => WebSocketUrl;

    IResourceBuilder<ParameterResource>? ILiveKitServerResource.ApiKey => ApiKey;

    IResourceBuilder<ParameterResource>? ILiveKitServerResource.ApiSecret => ApiSecret;

    internal ReferenceExpression? RedisConfiguration { get; set; }

    internal string? AdditionalConfiguration { get; set; }

    internal LiveKitServerUpstreamConfiguration? Configuration { get; set; }

    internal int RtcTcpPort { get; }

    internal int RtcUdpPort { get; }

    internal bool IsDevelopment { get; }

    internal int? TurnUdpPort { get; set; }

    internal int? TurnTlsPort { get; set; }

    internal string? TurnDomain { get; set; }

    internal string? TurnCertificateFile { get; set; }

    internal string? TurnKeyFile { get; set; }

    internal string? RtmpBaseUrl { get; set; }

    internal string? WhipBaseUrl { get; set; }
}
