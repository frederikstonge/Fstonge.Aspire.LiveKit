using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class JsonLogParserTests
{
    [Fact]
    public void TryParse_AddsCommonLineAndErrorStreamProperties()
    {
        using var document = JsonDocument.Parse("""{"message":"worker failed","levelname":"ERROR"}""");
        var parser = new PythonAgentJsonLogParser();

        var parsed = parser.TryParse(
            document.RootElement,
            document.RootElement.GetRawText(),
            lineNumber: 17,
            isErrorMessage: true,
            resourceTimestamp: null,
            out var record);

        Assert.True(parsed);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal("worker failed", record.Message);
        Assert.Equal(17, record.Properties["livekit.log.line_number"]);
        Assert.Equal(true, record.Properties["aspire.log.is_error_stream"]);
    }

    [Fact]
    public void TryParse_RejectsNonObjectJson()
    {
        using var document = JsonDocument.Parse("""["not", "an", "object"]""");
        var parser = new GenericJsonLogParser();

        var parsed = parser.TryParse(
            document.RootElement,
            document.RootElement.GetRawText(),
            lineNumber: 1,
            isErrorMessage: false,
            resourceTimestamp: null,
            out _);

        Assert.False(parsed);
    }
}
