using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit;

/// <summary>
/// Represents a LiveKit Ingress service container resource in a .NET Aspire distributed application.
/// </summary>
[AspireExport]
public sealed class LiveKitIngressResource : LiveKitContainerResource
{
    internal LiveKitIngressResource(string name)
        : base(name)
    {
    }

    internal LiveKitIngressConfiguration? Configuration { get; set; }

    internal string? AdditionalConfiguration { get; set; }
}
