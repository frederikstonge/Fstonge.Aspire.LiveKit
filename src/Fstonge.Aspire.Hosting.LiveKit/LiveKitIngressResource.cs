using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

public sealed class LiveKitIngressResource(string name) : LiveKitContainerResource(name)
{
    internal LiveKitIngressConfiguration? Configuration { get; set; }

    internal string? AdditionalConfiguration { get; set; }
}
