using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

public abstract class LiveKitContainerResource(string name) : ContainerResource(name)
{
    internal bool StructuredLogForwardingEnabled { get; set; }

    internal bool JsonLoggingEnabled { get; set; }
}
