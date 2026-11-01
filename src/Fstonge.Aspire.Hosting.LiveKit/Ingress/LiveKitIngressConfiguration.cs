namespace Aspire.Hosting;

/// <summary>
/// Represents configuration options for the LiveKit Ingress service.
/// </summary>
public sealed class LiveKitIngressConfiguration
{
    /// <summary>
    /// Gets or sets CPU admission-cost estimates for RTMP, WHIP, and URL inputs. Default: each cost is <c>0</c>;
    /// Ingress warns that these are below its minimums.
    /// </summary>
    public LiveKitIngressCpuCostConfiguration CpuCost { get; set; } = new();

    /// <summary>
    /// Gets or sets configuration for Ingress log verbosity. Default: <c>info</c> in the service logger.
    /// </summary>
    public LiveKitServiceLoggingConfiguration Logging { get; set; } = new();

    internal string ToYaml(bool jsonLoggingEnabled)
    {
        var yaml = new Dictionary<string, object?>();
        var cpuCost = CpuCost.ToDictionary();
        if (cpuCost.Count > 0)
        {
            yaml["cpu_cost"] = cpuCost;
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
