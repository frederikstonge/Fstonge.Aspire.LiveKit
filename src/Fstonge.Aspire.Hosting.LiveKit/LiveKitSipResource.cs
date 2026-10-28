using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

/// <summary>
/// Represents a LiveKit SIP service container resource in a .NET Aspire distributed application.
/// </summary>
public sealed class LiveKitSipResource : LiveKitContainerResource
{
    internal LiveKitSipResource(
        string name,
        LiveKitServerResource server,
        int sipPort,
        int rtpPortStart,
        int rtpPortEnd,
        int? healthPort,
        int? prometheusPort)
        : base(name)
    {
        Server = server;
        SipPort = sipPort;
        RtpPortStart = rtpPortStart;
        RtpPortEnd = rtpPortEnd;
        HealthPort = healthPort;
        PrometheusPort = prometheusPort;
    }

    internal LiveKitServerResource Server { get; }

    internal int SipPort { get; }

    internal int RtpPortStart { get; }

    internal int RtpPortEnd { get; }

    internal int? HealthPort { get; }

    internal int? PrometheusPort { get; }

    internal LiveKitSipConfiguration? Configuration { get; set; }

    internal int? TlsPort { get; set; }

    internal string? TlsCertificateFile { get; set; }

    internal string? TlsKeyFile { get; set; }

    internal string? AdditionalConfiguration { get; set; }
}
