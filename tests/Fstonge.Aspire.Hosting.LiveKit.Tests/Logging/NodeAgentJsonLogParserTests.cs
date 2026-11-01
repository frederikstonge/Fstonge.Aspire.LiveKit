using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class NodeAgentJsonLogParserTests
{
    private readonly LiveKitLogParser parser = new();

    [Theory]
    [InlineData(10, LogLevel.Trace)]
    [InlineData(20, LogLevel.Debug)]
    [InlineData(30, LogLevel.Information)]
    [InlineData(40, LogLevel.Warning)]
    [InlineData(50, LogLevel.Error)]
    [InlineData(60, LogLevel.Critical)]
    public void Parse_MapsNumericNodeAgentLogLevels(int numericLevel, LogLevel expectedLevel)
    {
        var record = parser.Parse(
            $$"""{"level":{{numericLevel}},"time":1767323045500,"msg":"node agent event","pid":123,"hostname":"worker"}""",
            isErrorMessage: false,
            lineNumber: 14);

        Assert.Equal(expectedLevel, record.Level);
        Assert.Equal("node agent event", record.Message);
        Assert.Equal(1767323045500L, record.Properties["livekit.timestamp"]);
        Assert.Equal(123L, record.Properties["pid"]);
        Assert.Equal("worker", record.Properties["hostname"]);
    }
}
