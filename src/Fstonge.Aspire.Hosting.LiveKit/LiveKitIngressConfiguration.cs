namespace Fstonge.Aspire.Hosting.LiveKit;

public sealed class LiveKitIngressConfiguration
{
    public LiveKitIngressCpuCostConfiguration? CpuCost { get; init; }

    public LiveKitServiceLoggingConfiguration? Logging { get; init; }

    internal string ToYaml(bool jsonLoggingEnabled)
    {
        var yaml = new Dictionary<string, object?>();
        if (CpuCost is not null)
        {
            yaml["cpu_cost"] = CpuCost.ToDictionary();
        }

        var logging = LiveKitConfigurationYaml.CreateLogging(Logging, jsonLoggingEnabled);
        if (logging is not null)
        {
            yaml["logging"] = logging;
        }

        if (yaml.Count == 0)
        {
            throw new InvalidOperationException("At least one Ingress configuration setting must be provided.");
        }

        return LiveKitConfigurationYaml.Serialize(yaml);
    }
}

public sealed class LiveKitIngressCpuCostConfiguration
{
    public double? RtmpCpuCost { get; init; }

    public double? WhipCpuCost { get; init; }

    public double? WhipBypassTranscodingCpuCost { get; init; }

    public double? UrlCpuCost { get; init; }

    public double? MinIdleRatio { get; init; }

    internal IDictionary<string, object?> ToDictionary()
    {
        var settings = new Dictionary<string, object?>();
        AddSetting(settings, "rtmp_cpu_cost", RtmpCpuCost);
        AddSetting(settings, "whip_cpu_cost", WhipCpuCost);
        AddSetting(settings, "whip_bypass_transcoding_cpu_cost", WhipBypassTranscodingCpuCost);
        AddSetting(settings, "url_cpu_cost", UrlCpuCost);
        AddSetting(settings, "min_idle_ratio", MinIdleRatio);
        if (settings.Count == 0)
        {
            throw new InvalidOperationException("At least one Ingress CPU cost setting must be provided.");
        }

        return settings;
    }

    private static void AddSetting(IDictionary<string, object?> settings, string name, double? value)
    {
        if (value is not { } setting)
        {
            return;
        }

        if (!double.IsFinite(setting) || setting < 0)
        {
            throw new ArgumentOutOfRangeException(
                name,
                "Ingress CPU cost settings must be finite, non-negative numbers.");
        }

        if (name == "min_idle_ratio" && (setting <= 0 || setting > 1))
        {
            throw new ArgumentOutOfRangeException(
                name,
                "The Ingress minimum idle CPU ratio must be between 0 and 1.");
        }

        settings[name] = setting;
    }
}
