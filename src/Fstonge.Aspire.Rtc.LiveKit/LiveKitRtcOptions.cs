namespace Fstonge.Aspire.Rtc.LiveKit;

/// <summary>
/// Options for configuring the LiveKit WebRTC client connection.
/// </summary>
public sealed class LiveKitRtcOptions
{
    /// <summary>
    /// The default configuration section name for LiveKit options.
    /// </summary>
    public const string SectionName = "LiveKit";

    /// <summary>
    /// Gets or sets the LiveKit server URL (HTTP, HTTPS, WS, or WSS).
    /// </summary>
    public string? Url { get; set; }
}
