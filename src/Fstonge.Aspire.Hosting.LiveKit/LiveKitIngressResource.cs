using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

public sealed class LiveKitIngressResource(string name) : ContainerResource(name, "livekit/ingress");
