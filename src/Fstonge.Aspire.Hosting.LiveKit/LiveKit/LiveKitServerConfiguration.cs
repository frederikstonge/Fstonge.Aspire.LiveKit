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

/// <summary>
/// Configures RTC network settings and port ranges for the LiveKit server.
/// </summary>
public sealed class LiveKitServerRtcConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether public-IP discovery is enabled for RTC. Default: <see langword="false"/>.
    /// </summary>
    public bool? UseExternalIp { get; set; }

    /// <summary>
    /// Gets or sets the first UDP port in the RTC media range. Default: unset. Because Aspire configures
    /// <c>rtc.udp_port</c>, LiveKit does not apply its <c>50000</c>-<c>60000</c> port-range fallback.
    /// Must be set together with <see cref="PortRangeEnd"/>.
    /// </summary>
    public int? PortRangeStart { get; set; }

    /// <summary>
    /// Gets or sets the last UDP port in the RTC media range. Default: unset. Because Aspire configures
    /// <c>rtc.udp_port</c>, LiveKit does not apply its <c>50000</c>-<c>60000</c> port-range fallback.
    /// Must be set together with <see cref="PortRangeStart"/>.
    /// </summary>
    public int? PortRangeEnd { get; set; }

    internal IDictionary<string, object?> ToDictionary()
    {
        var configuration = new Dictionary<string, object?>();
        if (UseExternalIp is { } useExternalIp)
        {
            configuration["use_external_ip"] = useExternalIp;
        }

        if (PortRangeStart is { } startValue)
        {
            configuration["port_range_start"] = startValue;
        }

        if (PortRangeEnd is { } endValue)
        {
            configuration["port_range_end"] = endValue;
        }

        if ((PortRangeStart is null) != (PortRangeEnd is null))
        {
            throw new InvalidOperationException("Both RTC port range bounds must be configured together.");
        }

        if (PortRangeStart is { } start && PortRangeEnd is { } end)
        {
            if (start is <= 0 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(PortRangeStart), "RTC port numbers must be between 1 and 65535.");
            }

            if (end < start || end > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(PortRangeEnd), "The RTC port range end must be between its start and 65535.");
            }
        }

        return configuration;
    }
}
