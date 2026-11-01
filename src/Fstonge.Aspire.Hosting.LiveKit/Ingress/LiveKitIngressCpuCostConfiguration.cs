namespace Aspire.Hosting;

/// <summary>
/// Configures CPU admission cost settings and idle thresholds for the LiveKit Ingress service.
/// </summary>
public sealed class LiveKitIngressCpuCostConfiguration
{
    /// <summary>
    /// Gets or sets the CPU units reserved per RTMP input. Default: <c>0</c>; Ingress recommends <c>2</c> and warns below <c>1</c>.
    /// </summary>
    public double? RtmpCpuCost { get; set; }

    /// <summary>
    /// Gets or sets the CPU units reserved per transcoded WHIP input. Default: <c>0</c>; Ingress recommends <c>2</c> and warns below <c>1</c>.
    /// </summary>
    public double? WhipCpuCost { get; set; }

    /// <summary>
    /// Gets or sets the CPU units reserved per WHIP input when transcoding is bypassed. Default: <c>0</c>;
    /// Ingress recommends <c>0.1</c> and warns below <c>0.05</c>.
    /// </summary>
    public double? WhipBypassTranscodingCpuCost { get; set; }

    /// <summary>
    /// Gets or sets the CPU units reserved per URL input. Default: <c>0</c>; Ingress recommends <c>2</c> and warns below <c>1</c>.
    /// </summary>
    public double? UrlCpuCost { get; set; }

    /// <summary>
    /// Gets or sets the target fraction of CPU to keep idle when admitting inputs. Default: <c>0.3</c> (30%).
    /// </summary>
    public double? MinIdleRatio { get; set; }

    internal IDictionary<string, object?> ToDictionary()
    {
        var settings = new Dictionary<string, object?>();
        AddSetting(settings, "rtmp_cpu_cost", RtmpCpuCost);
        AddSetting(settings, "whip_cpu_cost", WhipCpuCost);
        AddSetting(settings, "whip_bypass_transcoding_cpu_cost", WhipBypassTranscodingCpuCost);
        AddSetting(settings, "url_cpu_cost", UrlCpuCost);
        AddSetting(settings, "min_idle_ratio", MinIdleRatio);
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
