using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class PythonAgentJsonLogParserTests
{
    private readonly LiveKitLogParser parser = new();

    [Fact]
    public void Parse_ForwardsPythonAgentJson()
    {
        var record = parser.Parse(
            """{"message":"worker started","levelname":"INFO","name":"livekit.agents","asctime":"2026-01-02 03:04:05","job_id":"job-1","context":{"room":"room-1"}}""",
            isErrorMessage: false,
            lineNumber: 7);

        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("worker started", record.Message);
        Assert.Equal("livekit.agents", record.Properties["name"]);
        Assert.Equal("2026-01-02 03:04:05", record.Properties["livekit.timestamp"]);
        Assert.Equal("job-1", record.Properties["job_id"]);
        Assert.Equal("""{"room":"room-1"}""", record.Properties["context"]);
    }

    [Theory]
    [InlineData("DEBUG", LogLevel.Debug)]
    [InlineData("INFO", LogLevel.Information)]
    [InlineData("WARNING", LogLevel.Warning)]
    [InlineData("ERROR", LogLevel.Error)]
    [InlineData("CRITICAL", LogLevel.Critical)]
    public void Parse_MapsPythonAgentLevelNames(string levelName, LogLevel expectedLevel)
    {
        var record = parser.Parse(
            $$"""{"message":"python event","levelname":"{{levelName}}","name":"worker"}""",
            isErrorMessage: false,
            lineNumber: 8);

        Assert.Equal(expectedLevel, record.Level);
        Assert.Equal("python event", record.Message);
        Assert.Equal("worker", record.Properties["name"]);
    }
}
