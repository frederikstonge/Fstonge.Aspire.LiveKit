using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class LiveKitServerJsonLogParserTests
{
    private readonly LiveKitLogParser parser = new();

    [Fact]
    public void Parse_ForwardsLiveKitServerJson()
    {
        var record = parser.Parse(
            """{"time":"2026-01-02T03:04:05Z","level":"warn","msg":"room disconnected","room":"room-1","attempt":2}""",
            isErrorMessage: false,
            lineNumber: 12);

        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal("room disconnected", record.Message);
        Assert.Equal(12, record.Properties["livekit.log.line_number"]);
        Assert.Equal("2026-01-02T03:04:05Z", record.Properties["livekit.timestamp"]);
        Assert.Equal("room-1", record.Properties["room"]);
        Assert.Equal(2L, record.Properties["attempt"]);
    }
}
