using Aspire.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class LiveKitConfigurationYamlTests
{
    [Fact]
    public void CreateLogging_CombinesLevelAndJsonFlag()
    {
        var settings = LiveKitConfigurationYaml.CreateLogging(
            new LiveKitServiceLoggingConfiguration { Level = "WARN" },
            jsonLoggingEnabled: true);

        Assert.NotNull(settings);
        Assert.Equal("warn", settings["level"]);
        Assert.Equal(true, settings["json"]);
    }

    [Fact]
    public void CreateLogging_ReturnsNullWhenNoSettingsAreEnabled()
    {
        Assert.Null(LiveKitConfigurationYaml.CreateLogging(null, jsonLoggingEnabled: false));
    }

    [Fact]
    public void SerializeScalar_QuotesSpecialCharacters()
    {
        var value = LiveKitConfigurationYaml.SerializeScalar("value: #1");

        Assert.Contains("value: #1", value);
        Assert.StartsWith("'", value);
        Assert.EndsWith("'", value);
    }

    [Fact]
    public void ValidateAdditionalConfiguration_AddsTrailingNewline()
    {
        var yaml = LiveKitConfigurationYaml.ValidateAdditionalConfiguration(
            "custom_setting: true",
            "yaml",
            "managed");

        Assert.Equal("custom_setting: true\n", yaml);
    }

    [Theory]
    [InlineData("")]
    [InlineData("setting: [")]
    [InlineData("- item")]
    [InlineData("one: 1\nONE: 2")]
    [InlineData("managed: true")]
    [InlineData("---\ncustom: true\n---\nother: true")]
    public void ValidateAdditionalConfiguration_RejectsInvalidOrReservedYaml(string yaml)
    {
        Assert.Throws<ArgumentException>(() =>
            LiveKitConfigurationYaml.ValidateAdditionalConfiguration(yaml, "yaml", "managed"));
    }

    [Fact]
    public void ValidateLogLevel_RejectsUnsupportedLevel()
    {
        Assert.Throws<ArgumentException>(() => LiveKitConfigurationYaml.ValidateLogLevel("trace", "level"));
    }
}
