using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class LiveKitLogParserTests
{
    private readonly LiveKitLogParser parser = new();

    [Fact]
    public void Parse_UsesOuterTimestampForJsonWithoutItsOwnTimestamp()
    {
        var record = parser.Parse(
            """2026-01-02T03:04:05Z {"message":"agent ready","level":"debug"}""",
            isErrorMessage: false,
            lineNumber: 5);

        Assert.Equal(LogLevel.Debug, record.Level);
        Assert.Equal("agent ready", record.Message);
        Assert.Equal(
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            record.Properties["livekit.timestamp"]);
    }

    [Fact]
    public void Parse_FallsBackToPlainTextForNonObjectJson()
    {
        const string content = """["not", "a", "log record"]""";

        var record = parser.Parse(content, isErrorMessage: false, lineNumber: 1);

        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(content, record.Message);
    }
}
