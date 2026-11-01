using Aspire.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Sip;

public sealed class LiveKitSipConfigurationTests
{
    [Fact]
    public void ToYaml_EmitsConfiguredSettingsAndLogging()
    {
        var configuration = new LiveKitSipConfiguration
        {
            ClusterId = "sip-cluster",
            MaxCpuUtilization = 0.8,
            MaxActiveCalls = 100,
            UseExternalIp = true,
            LocalNet = "10.0.0.0/8",
            MediaUseExternalIp = true,
            UdpMaxPayload = 1300,
            EnableJitterBuffer = true,
            EnableJitterBufferProbability = 0.5,
            Logging = new LiveKitServiceLoggingConfiguration { Level = "warn" },
        };

        var yaml = configuration.ToYaml(jsonLoggingEnabled: true);

        Assert.Contains("cluster_id: sip-cluster", yaml);
        Assert.Contains("max_cpu_utilization: 0.8", yaml);
        Assert.Contains("max_active_calls: 100", yaml);
        Assert.Contains("use_external_ip: true", yaml);
        Assert.Contains("local_net: 10.0.0.0/8", yaml);
        Assert.Contains("media_use_external_ip: true", yaml);
        Assert.Contains("udp_max_payload: 1300", yaml);
        Assert.Contains("enable_jitter_buffer: true", yaml);
        Assert.Contains("enable_jitter_buffer_prob: 0.5", yaml);
        Assert.Contains("level: warn", yaml);
        Assert.Contains("json: true", yaml);
    }

    [Fact]
    public void ToYaml_RejectsEmptyConfiguration()
    {
        Assert.Throws<InvalidOperationException>(() => new LiveKitSipConfiguration().ToYaml(false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ToYaml_RejectsInvalidMaxCpuUtilization(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LiveKitSipConfiguration { MaxCpuUtilization = value }.ToYaml(false));
    }

    [Fact]
    public void ToYaml_RejectsNegativeMaxActiveCallsAndNonPositiveUdpPayload()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LiveKitSipConfiguration { MaxActiveCalls = -1 }.ToYaml(false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LiveKitSipConfiguration { UdpMaxPayload = 0 }.ToYaml(false));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ToYaml_RejectsInvalidJitterProbability(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LiveKitSipConfiguration { EnableJitterBufferProbability = value }.ToYaml(false));
    }

    [Fact]
    public void ToYaml_RejectsConflictingNatAndExternalIpOptions()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new LiveKitSipConfiguration { UseExternalIp = true, Nat1To1Ip = "203.0.113.1" }.ToYaml(false));
        Assert.Throws<InvalidOperationException>(() =>
            new LiveKitSipConfiguration { MediaUseExternalIp = true, MediaNat1To1Ip = "203.0.113.1" }.ToYaml(false));
    }
}
