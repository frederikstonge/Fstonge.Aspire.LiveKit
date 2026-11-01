using Aspire.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.LiveKit;

public sealed class LiveKitServerConfigurationTests
{
    [Theory]
    [InlineData("DEBUG", "debug")]
    [InlineData("info", "info")]
    [InlineData("Warn", "warn")]
    [InlineData("ERROR", "error")]
    public void ToDictionary_NormalizesSupportedLogLevels(string input, string expected)
    {
        var configuration = new LiveKitServerConfiguration { LogLevel = input };

        Assert.Equal(expected, configuration.ToDictionary()["log_level"]);
    }

    [Theory]
    [InlineData("trace")]
    [InlineData("")]
    [InlineData("fatal")]
    public void ToDictionary_RejectsUnsupportedLogLevel(string level)
    {
        var configuration = new LiveKitServerConfiguration { LogLevel = level };

        Assert.Throws<ArgumentException>(() => configuration.ToDictionary());
    }

    [Fact]
    public void ToDictionary_OmitsUnsetLogLevel()
    {
        var configuration = new LiveKitServerConfiguration();

        Assert.Empty(configuration.ToDictionary());
    }
}
