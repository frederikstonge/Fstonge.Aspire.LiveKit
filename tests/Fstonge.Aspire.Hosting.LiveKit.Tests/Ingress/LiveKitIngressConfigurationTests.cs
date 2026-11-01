using Aspire.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Ingress;

public sealed class LiveKitIngressConfigurationTests
{
    [Fact]
    public void ToYaml_EmitsCpuCostsAndLogging()
    {
        var configuration = new LiveKitIngressConfiguration
        {
            CpuCost = new LiveKitIngressCpuCostConfiguration { RtmpCpuCost = 2, MinIdleRatio = 0.3 },
            Logging = new LiveKitServiceLoggingConfiguration { Level = "warn" },
        };

        var yaml = configuration.ToYaml(jsonLoggingEnabled: true);

        Assert.Contains("cpu_cost:", yaml);
        Assert.Contains("rtmp_cpu_cost: 2", yaml);
        Assert.Contains("min_idle_ratio: 0.3", yaml);
        Assert.Contains("level: warn", yaml);
        Assert.Contains("json: true", yaml);
    }

    [Fact]
    public void ToYaml_RejectsEmptyConfiguration()
    {
        Assert.Throws<InvalidOperationException>(() => new LiveKitIngressConfiguration().ToYaml(false));
    }
}
