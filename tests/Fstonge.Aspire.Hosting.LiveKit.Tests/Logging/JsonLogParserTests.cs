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

    [Theory]
    [InlineData(-1, LogLevel.Trace)]
    [InlineData(0, LogLevel.Trace)]
    [InlineData(1, LogLevel.Debug)]
    [InlineData(10, LogLevel.Debug)]
    [InlineData(11, LogLevel.Information)]
    [InlineData(20, LogLevel.Information)]
    [InlineData(21, LogLevel.Warning)]
    [InlineData(30, LogLevel.Warning)]
    [InlineData(31, LogLevel.Error)]
    [InlineData(40, LogLevel.Error)]
    [InlineData(41, LogLevel.Critical)]
    public void TryParse_MapsNumericGenericLogLevels(int numericLevel, LogLevel expectedLevel)
    {
        using var document = JsonDocument.Parse(
            $$"""{"message":"worker event","level":{{numericLevel}},"flag":false}""");
        var parser = new GenericJsonLogParser();

        var parsed = parser.TryParse(
            document.RootElement,
            document.RootElement.GetRawText(),
            lineNumber: 1,
            isErrorMessage: false,
            resourceTimestamp: null,
            out var record);

        Assert.True(parsed);
        Assert.Equal(expectedLevel, record.Level);
        Assert.Equal(false, record.Properties["flag"]);
    }

    [Theory]
    [InlineData("trace", LogLevel.Trace)]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("info", LogLevel.Information)]
    [InlineData("information", LogLevel.Information)]
    [InlineData("warn", LogLevel.Warning)]
    [InlineData("warning", LogLevel.Warning)]
    [InlineData("error", LogLevel.Error)]
    [InlineData("err", LogLevel.Error)]
    [InlineData("fatal", LogLevel.Critical)]
    [InlineData("panic", LogLevel.Critical)]
    [InlineData("critical", LogLevel.Critical)]
    public void TryParse_MapsGenericStringLogLevels(string level, LogLevel expectedLevel)
    {
        using var document = JsonDocument.Parse(
            $$"""{"message":"worker event","level":"{{level}}"}""");
        var parser = new GenericJsonLogParser();

        var parsed = parser.TryParse(
            document.RootElement,
            document.RootElement.GetRawText(),
            lineNumber: 1,
            isErrorMessage: false,
            resourceTimestamp: null,
            out var record);

        Assert.True(parsed);
        Assert.Equal(expectedLevel, record.Level);
    }

    [Fact]
    public void TryParse_UsesInformationForNonStringNonIntegerLevels()
    {
        using var document = JsonDocument.Parse("""{"message":"worker event","level":true}""");
        var parser = new GenericJsonLogParser();

        var parsed = parser.TryParse(
            document.RootElement,
            document.RootElement.GetRawText(),
            lineNumber: 1,
            isErrorMessage: false,
            resourceTimestamp: null,
            out var record);

        Assert.True(parsed);
        Assert.Equal(LogLevel.Information, record.Level);
    }
}
