using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class LiveKitTabDelimitedLogParserTests
{
    private readonly LiveKitLogParser parser = new();

    [Fact]
    public void Parse_ForwardsLiveKitTabDelimitedLogWithoutAttributes()
    {
        var record = parser.Parse(
            "2026-01-02T03:04:05Z\tDEBUG\tlivekit.room\troom.go:12\troom connected",
            isErrorMessage: false,
            lineNumber: 11);

        Assert.Equal(LogLevel.Debug, record.Level);
        Assert.Equal("room connected", record.Message);
        Assert.Equal("livekit.room", record.Properties["livekit.logger"]);
        Assert.Equal("room.go:12", record.Properties["livekit.caller"]);
        Assert.Equal(DateTimeOffset.Parse("2026-01-02T03:04:05Z"), record.Properties["livekit.timestamp"]);
        Assert.False(record.Properties.ContainsKey("aspire.timestamp"));
    }

    [Fact]
    public void Parse_ForwardsLiveKitTabDelimitedLogAndAspireTimestamp()
    {
        var record = parser.Parse(
            "2026-01-02T03:04:05Z 2026-01-02T03:04:06Z\tERROR\tlivekit.room\troom.go:42\troom disconnected\t{\"room\":\"room-1\"}",
            isErrorMessage: false,
            lineNumber: 9);

        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal("room disconnected", record.Message);
        Assert.Equal("livekit.room", record.Properties["livekit.logger"]);
        Assert.Equal("room.go:42", record.Properties["livekit.caller"]);
        Assert.Equal("room-1", record.Properties["room"]);
        Assert.Equal(
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            record.Properties["aspire.timestamp"]);
    }

    [Fact]
    public void Parse_FallsBackToPlainTextWhenAttributesAreMalformed()
    {
        var content = "2026-01-02T03:04:05Z\tERROR\tlivekit.room\troom.go:12\trequest failed\t{bad json}";

        var record = parser.Parse(content, isErrorMessage: false, lineNumber: 23);

        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal(content, record.Message);
        Assert.False(record.Properties.ContainsKey("livekit.timestamp"));
    }
}
