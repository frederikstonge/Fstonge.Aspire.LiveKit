using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class PlainTextLogParserTests
{
    private readonly LiveKitLogParser parser = new();

    [Theory]
    [InlineData("DEBUG: worker ready", LogLevel.Debug)]
    [InlineData("[WARN] retrying connection", LogLevel.Warning)]
    [InlineData("request\tERROR\trequest failed", LogLevel.Error)]
    [InlineData("unclassified output", LogLevel.Information)]
    public void Parse_MapsPlainTextPrefixes(string content, LogLevel expectedLevel)
    {
        var record = parser.Parse(content, isErrorMessage: false, lineNumber: 22);

        Assert.Equal(expectedLevel, record.Level);
        Assert.Equal(content, record.Message);
        Assert.Equal(false, record.Properties["aspire.log.is_error_stream"]);
    }

    [Fact]
    public void Parse_FallsBackToPlainTextForMalformedJson()
    {
        const string content = "ERROR: {malformed json";

        var record = parser.Parse(content, isErrorMessage: true, lineNumber: 21);

        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal(content, record.Message);
        Assert.Equal(true, record.Properties["aspire.log.is_error_stream"]);
        Assert.Equal(21, record.Properties["livekit.log.line_number"]);
    }

    [Fact]
    public void Parse_AddsTimestampToTimestampedPlainText()
    {
        var timestamp = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
        var record = parser.Parse(
            "2026-01-02T03:04:05Z ERROR: worker stopped",
            isErrorMessage: false,
            lineNumber: 23);

        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal(timestamp, record.Properties["livekit.timestamp"]);
    }
}
