using Aspire.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.LiveKit;

public sealed class LiveKitServerRtcConfigurationTests
{
    [Fact]
    public void ToDictionary_EmitsConfiguredRtcSettings()
    {
        var configuration = new LiveKitServerRtcConfiguration
        {
            UseExternalIp = true,
            PortRangeStart = 50000,
            PortRangeEnd = 60000,
        };

        var settings = configuration.ToDictionary();

        Assert.Equal(true, settings["use_external_ip"]);
        Assert.Equal(50000, settings["port_range_start"]);
        Assert.Equal(60000, settings["port_range_end"]);
    }

    [Fact]
    public void ToDictionary_RejectsIncompletePortRange()
    {
        var configuration = new LiveKitServerRtcConfiguration { PortRangeStart = 50000 };

        Assert.Throws<InvalidOperationException>(() => configuration.ToDictionary());
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 65536)]
    [InlineData(101, 100)]
    public void ToDictionary_RejectsInvalidPortRange(int start, int end)
    {
        var configuration = new LiveKitServerRtcConfiguration
        {
            PortRangeStart = start,
            PortRangeEnd = end,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => configuration.ToDictionary());
    }
}
