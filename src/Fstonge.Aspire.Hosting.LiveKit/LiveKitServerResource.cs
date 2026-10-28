using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

/// <summary>
/// Represents a LiveKit server container resource in a .NET Aspire distributed application.
/// </summary>
public sealed class LiveKitServerResource : LiveKitContainerResource
{
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

    internal ReferenceExpression? RedisConfiguration { get; set; }

    internal string? AdditionalConfiguration { get; set; }

    internal LiveKitServerConfiguration? Configuration { get; set; }

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
