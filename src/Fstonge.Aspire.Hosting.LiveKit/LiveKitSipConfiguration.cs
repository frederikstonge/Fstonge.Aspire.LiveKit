namespace Fstonge.Aspire.Hosting.LiveKit;

public sealed class LiveKitSipConfiguration
{
    public string? ClusterId { get; init; }

    public double? MaxCpuUtilization { get; init; }

    public int? MaxActiveCalls { get; init; }

    public bool? UseExternalIp { get; init; }

    public string? LocalNet { get; init; }

    public string? Nat1To1Ip { get; init; }

    public bool? MediaUseExternalIp { get; init; }

    public string? MediaNat1To1Ip { get; init; }

    public int? UdpMaxPayload { get; init; }

    public bool? EnableJitterBuffer { get; init; }

    public double? EnableJitterBufferProbability { get; init; }

    public LiveKitServiceLoggingConfiguration? Logging { get; init; }

    internal string ToYaml(bool jsonLoggingEnabled)
    {
        if (MaxCpuUtilization is { } maxCpuUtilization
            && (maxCpuUtilization <= 0 || maxCpuUtilization > 1 || !double.IsFinite(maxCpuUtilization)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxCpuUtilization),
                "Maximum CPU utilization must be greater than 0 and no greater than 1.");
        }

        if (MaxActiveCalls is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxActiveCalls), "Maximum active calls cannot be negative.");
        }

        if (UdpMaxPayload is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(UdpMaxPayload), "The maximum UDP payload must be positive.");
        }

        if (EnableJitterBufferProbability is { } probability
            && (!double.IsFinite(probability) || probability is < 0 or > 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(EnableJitterBufferProbability),
                "Jitter buffer probability must be between 0 and 1.");
        }

        if (UseExternalIp == true && !string.IsNullOrWhiteSpace(Nat1To1Ip))
        {
            throw new InvalidOperationException("UseExternalIp and Nat1To1Ip cannot both be configured.");
        }

        if (MediaUseExternalIp == true && !string.IsNullOrWhiteSpace(MediaNat1To1Ip))
        {
            throw new InvalidOperationException("MediaUseExternalIp and MediaNat1To1Ip cannot both be configured.");
        }

        var yaml = new Dictionary<string, object?>();
        AddString(yaml, "cluster_id", ClusterId);
        AddNumber(yaml, "max_cpu_utilization", MaxCpuUtilization);
        AddInteger(yaml, "max_active_calls", MaxActiveCalls);
        AddBoolean(yaml, "use_external_ip", UseExternalIp);
        AddString(yaml, "local_net", LocalNet);
        AddString(yaml, "nat_1_to_1_ip", Nat1To1Ip);
        AddBoolean(yaml, "media_use_external_ip", MediaUseExternalIp);
        AddString(yaml, "media_nat_1_to_1_ip", MediaNat1To1Ip);
        AddInteger(yaml, "udp_max_payload", UdpMaxPayload);
        AddBoolean(yaml, "enable_jitter_buffer", EnableJitterBuffer);
        AddNumber(yaml, "enable_jitter_buffer_prob", EnableJitterBufferProbability);
        var logging = LiveKitConfigurationYaml.CreateLogging(Logging, jsonLoggingEnabled);
        if (logging is not null)
        {
            yaml["logging"] = logging;
        }

        if (yaml.Count == 0)
        {
            throw new InvalidOperationException("At least one SIP configuration setting must be provided.");
        }

        return LiveKitConfigurationYaml.Serialize(yaml);
    }

    private static void AddString(IDictionary<string, object?> yaml, string name, string? value)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
            yaml[name] = value;
        }
    }

    private static void AddBoolean(IDictionary<string, object?> yaml, string name, bool? value)
    {
        if (value is { } setting)
        {
            yaml[name] = setting;
        }
    }

    private static void AddInteger(IDictionary<string, object?> yaml, string name, int? value)
    {
        if (value is { } setting)
        {
            yaml[name] = setting;
        }
    }

    private static void AddNumber(IDictionary<string, object?> yaml, string name, double? value)
    {
        if (value is { } setting)
        {
            if (!double.IsFinite(setting))
            {
                throw new ArgumentOutOfRangeException(name, "Configuration values must be finite numbers.");
            }

            yaml[name] = setting;
        }
    }
}
