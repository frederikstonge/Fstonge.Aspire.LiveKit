using Aspire.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Ingress;

public sealed class LiveKitIngressCpuCostConfigurationTests
{
    [Fact]
    public void ToDictionary_EmitsConfiguredCpuCosts()
    {
        var configuration = new LiveKitIngressCpuCostConfiguration
        {
            RtmpCpuCost = 2,
            WhipCpuCost = 1.5,
            WhipBypassTranscodingCpuCost = 0.1,
            UrlCpuCost = 3,
            MinIdleRatio = 0.3,
        };

        var settings = configuration.ToDictionary();

        Assert.Equal(2d, settings["rtmp_cpu_cost"]);
        Assert.Equal(1.5, settings["whip_cpu_cost"]);
        Assert.Equal(0.1, settings["whip_bypass_transcoding_cpu_cost"]);
        Assert.Equal(3d, settings["url_cpu_cost"]);
        Assert.Equal(0.3, settings["min_idle_ratio"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ToDictionary_RejectsNegativeOrNonFiniteCosts(double value)
    {
        var configuration = new LiveKitIngressCpuCostConfiguration { RtmpCpuCost = value };

        Assert.Throws<ArgumentOutOfRangeException>(() => configuration.ToDictionary());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void ToDictionary_RejectsInvalidMinimumIdleRatio(double value)
    {
        var configuration = new LiveKitIngressCpuCostConfiguration { MinIdleRatio = value };

        Assert.Throws<ArgumentOutOfRangeException>(() => configuration.ToDictionary());
    }
}
