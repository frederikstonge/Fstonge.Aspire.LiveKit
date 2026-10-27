using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

public sealed class LiveKitSipResource(
    string name,
    LiveKitServerResource server,
    int sipPort,
    int rtpPortStart,
    int rtpPortEnd,
    int? healthPort,
    int? prometheusPort)
    : ContainerResource(name)
{
    internal LiveKitServerResource Server { get; } = server;

    internal int SipPort { get; } = sipPort;

    internal int RtpPortStart { get; } = rtpPortStart;

    internal int RtpPortEnd { get; } = rtpPortEnd;

    internal int? HealthPort { get; } = healthPort;

    internal int? PrometheusPort { get; } = prometheusPort;

    internal int? TlsPort { get; set; }

    internal string? TlsCertificateFile { get; set; }

    internal string? TlsKeyFile { get; set; }

    internal string? AdditionalConfiguration { get; set; }
}
