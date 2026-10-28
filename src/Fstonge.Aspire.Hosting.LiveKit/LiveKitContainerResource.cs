using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

/// <summary>
/// Represents a containerized LiveKit resource in a .NET Aspire distributed application.
/// </summary>
public abstract class LiveKitContainerResource : ContainerResource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveKitContainerResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    protected LiveKitContainerResource(string name)
        : base(name)
    {
    }

    internal bool StructuredLogForwardingEnabled { get; set; }

    internal bool JsonLoggingEnabled { get; set; }
}
