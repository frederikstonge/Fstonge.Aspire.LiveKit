namespace Aspire.Hosting;

/// <summary>
/// Represents configuration options for the LiveKit server.
/// </summary>
public sealed class LiveKitServerConfiguration
{
    /// <summary>
    /// Gets or sets the server log verbosity. Default: <c>info</c>.
    /// </summary>
    public string? LogLevel { get; set; }

    /// <summary>
    /// Gets or sets the RTC address discovery and UDP port range configuration. When omitted, public-IP discovery is
    /// disabled and the port range is unset; Aspire configures RTC with its resource UDP port.
    /// </summary>
    public LiveKitServerRtcConfiguration Rtc { get; set; } = new();

    internal IDictionary<string, object?> ToDictionary()
    {
        var configuration = new Dictionary<string, object?>();
        if (LogLevel is not null)
        {
            configuration["log_level"] =
                LiveKitConfigurationYaml.ValidateLogLevel(LogLevel, nameof(LogLevel));
        }

        return configuration;
    }
}
