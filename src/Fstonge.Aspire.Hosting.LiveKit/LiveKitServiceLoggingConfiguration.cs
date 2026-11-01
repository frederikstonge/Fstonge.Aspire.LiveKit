namespace Aspire.Hosting;

/// <summary>
/// Configures logging for LiveKit companion services (Egress, Ingress, and SIP).
/// </summary>
public sealed class LiveKitServiceLoggingConfiguration
{
    /// <summary>
    /// Gets or sets the minimum log severity to emit. Accepted values are <c>debug</c>, <c>info</c>, <c>warn</c>, and <c>error</c>.
    /// Default: <c>info</c> for companion services.
    /// </summary>
    public string? Level { get; set; }
}
