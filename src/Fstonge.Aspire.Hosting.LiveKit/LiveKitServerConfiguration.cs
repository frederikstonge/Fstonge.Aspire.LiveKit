namespace Fstonge.Aspire.Hosting.LiveKit;

public sealed class LiveKitServerConfiguration
{
    public string? LogLevel { get; init; }

    public LiveKitServerRtcConfiguration? Rtc { get; init; }

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

    internal IDictionary<string, object?> ToRtcDictionary() => Rtc?.ToDictionary()
        ?? new Dictionary<string, object?>();
}

public sealed class LiveKitServerRtcConfiguration
{
    public bool? UseExternalIp { get; init; }

    public int? PortRangeStart { get; init; }

    public int? PortRangeEnd { get; init; }

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
