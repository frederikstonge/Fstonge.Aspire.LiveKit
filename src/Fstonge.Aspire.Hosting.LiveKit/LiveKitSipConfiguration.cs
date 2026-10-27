namespace Fstonge.Aspire.Hosting.LiveKit;

public sealed class LiveKitSipConfiguration
{
    /// <summary>
    /// Identifies the SIP cluster for routing. Default: empty (no cluster ID).
    /// </summary>
    public string? ClusterId { get; set; }

    /// <summary>
    /// Maximum CPU utilization allowed for accepting calls, from greater than 0 through 1.
    /// Default: <c>0.9</c>.
    /// </summary>
    public double? MaxCpuUtilization { get; set; }

    /// <summary>
    /// Maximum active calls used for affinity-based routing. Default: <c>0</c> (not set).
    /// </summary>
    public int? MaxActiveCalls { get; set; }

    /// <summary>
    /// Discovers the public signaling IP. Default: <see langword="false"/>.
    /// Cannot be combined with <see cref="Nat1To1Ip"/>.
    /// </summary>
    public bool? UseExternalIp { get; set; }

    /// <summary>
    /// Local network CIDR used for SIP address selection. Default: empty (no CIDR configured).
    /// </summary>
    public string? LocalNet { get; set; }

    /// <summary>
    /// Static one-to-one signaling NAT address. Default: empty (not configured).
    /// Cannot be combined with <see cref="UseExternalIp"/>.
    /// </summary>
    public string? Nat1To1Ip { get; set; }

    /// <summary>
    /// Discovers a public media IP separately from the signaling IP. Default: <see langword="false"/>.
    /// Cannot be combined with <see cref="MediaNat1To1Ip"/>.
    /// </summary>
    public bool? MediaUseExternalIp { get; set; }

    /// <summary>
    /// Static one-to-one media NAT address. Default: empty (not configured).
    /// Cannot be combined with <see cref="MediaUseExternalIp"/>.
    /// </summary>
    public string? MediaNat1To1Ip { get; set; }

    /// <summary>
    /// Maximum SIP UDP payload size in bytes. Default: <c>0</c> (the SIP config leaves this unset).
    /// </summary>
    public int? UdpMaxPayload { get; set; }

    /// <summary>
    /// Enables the media jitter buffer. Default: <see langword="false"/>.
    /// </summary>
    public bool? EnableJitterBuffer { get; set; }

    /// <summary>
    /// Probability threshold for enabling the jitter buffer, from 0 to 1. Default: <c>0</c>.
    /// </summary>
    public double? EnableJitterBufferProbability { get; set; }

    /// <summary>
    /// Configures SIP log verbosity and JSON output. Default log level: <c>info</c>; JSON output is disabled
    /// unless enabled by <c>WithJsonLogging</c> or <c>WithStructuredLogs</c>.
    /// </summary>
    public LiveKitServiceLoggingConfiguration? Logging { get; set; }

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
