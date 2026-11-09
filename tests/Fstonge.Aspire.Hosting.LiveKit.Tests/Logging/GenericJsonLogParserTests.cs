using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class GenericJsonLogParserTests
{
    private readonly LiveKitLogParser parser = new();

    [Fact]
    public void Parse_ForwardsGenericJsonFieldsAndConvertsJsonValues()
    {
        var record = parser.Parse(
            """{"body":"generic event","severity":"fatal","created":1767323045.5,"enabled":true,"optional":null,"context":{"room":"room-1"},"items":[1,2]}""",
            isErrorMessage: true,
            lineNumber: 10);

        Assert.Equal(LogLevel.Critical, record.Level);
        Assert.Equal("generic event", record.Message);
        Assert.Equal(1767323045.5, record.Properties["livekit.timestamp"]);
        Assert.Equal(true, record.Properties["enabled"]);
        Assert.Null(record.Properties["optional"]);
        Assert.Equal("""{"room":"room-1"}""", record.Properties["context"]);
        Assert.Equal("[1,2]", record.Properties["items"]);
        Assert.Equal(true, record.Properties["aspire.log.is_error_stream"]);
    }

    [Fact]
    public void Parse_PreservesJsonRecordsWithoutMessageField()
    {
        const string content = """{"request_id":"request-1"}""";

        var record = parser.Parse(content, isErrorMessage: false, lineNumber: 2);

        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(content, record.Message);
        Assert.Equal("request-1", record.Properties["request_id"]);
    }

    [Fact]
    public void Parse_MapsNumericPythonLogLevels()
    {
        var record = parser.Parse(
            """{"body":"request failed","severity":40,"created":1767323045.5}""",
            isErrorMessage: false,
            lineNumber: 3);

        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal("request failed", record.Message);
        Assert.Equal(1767323045.5, record.Properties["livekit.timestamp"]);
    }
}
