namespace Fstonge.Aspire.Sdk.LiveKit;

/// <summary>
/// Options for configuring the LiveKit server SDK client and API credentials.
/// </summary>
public sealed class LiveKitOptions
{
    /// <summary>
    /// The default configuration section name for LiveKit server SDK options.
    /// </summary>
    public const string SectionName = "LiveKit";

    /// <summary>
    /// Gets or sets the LiveKit server URL (HTTP or HTTPS).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the LiveKit API key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the LiveKit API secret key.
    /// </summary>
    public string? ApiSecret { get; set; }
}
