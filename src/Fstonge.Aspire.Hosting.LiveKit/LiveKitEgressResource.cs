using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

/// <summary>
/// Represents a LiveKit Egress service container resource in a .NET Aspire distributed application.
/// </summary>
public sealed class LiveKitEgressResource : LiveKitContainerResource
{
    internal LiveKitEgressResource(
        string name,
        LiveKitServerResource server,
        int? healthPort,
        int? prometheusPort)
        : base(name)
    {
        Server = server;
        HealthPort = healthPort;
        PrometheusPort = prometheusPort;
    }

    internal LiveKitServerResource Server { get; }

    internal int? HealthPort { get; }

    internal int? PrometheusPort { get; }

    internal LiveKitEgressConfiguration? Configuration { get; set; }

    internal string? AdditionalConfiguration { get; set; }

    internal string? StorageConfiguration { get; set; }

    internal string? LocalOutputContainerPath { get; set; }
}
