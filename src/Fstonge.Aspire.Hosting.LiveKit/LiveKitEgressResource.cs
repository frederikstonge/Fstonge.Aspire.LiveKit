using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

public sealed class LiveKitEgressResource(
    string name,
    LiveKitServerResource server,
    int? healthPort,
    int? prometheusPort)
    : ContainerResource(name, "livekit/egress")
{
    internal LiveKitServerResource Server { get; } = server;

    internal int? HealthPort { get; } = healthPort;

    internal int? PrometheusPort { get; } = prometheusPort;

    internal string? AdditionalConfiguration { get; set; }

    internal string? StorageConfiguration { get; set; }

    internal string? LocalOutputContainerPath { get; set; }
}
