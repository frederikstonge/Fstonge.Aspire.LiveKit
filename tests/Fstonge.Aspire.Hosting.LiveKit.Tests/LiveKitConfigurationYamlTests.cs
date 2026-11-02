using Aspire.Hosting;
using Aspire.Hosting.LiveKit.Configuration.Ingress;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class LiveKitConfigurationYamlTests
{
    [Fact]
    public void SerializeConfiguration_UsesUpstreamAliasesAndMergesJsonLogging()
    {
        var yaml = LiveKitConfigurationYaml.SerializeConfiguration(
            new LiveKitIngressUpstreamConfiguration
            {
                CpuCost = new CPUCostConfig { RtmpCpuCost = 2 },
                Logging = new Config_7837b7412944 { Level = "warn" },
            },
            "configuration",
            jsonLoggingEnabled: true,
            additionalYaml: null);

        Assert.Contains("cpu_cost:", yaml);
        Assert.Contains("rtmp_cpu_cost: 2", yaml);
        Assert.Contains("level: warn", yaml);
        Assert.Contains("json: true", yaml);
    }

    [Fact]
    public void SerializeConfiguration_RejectsAspireManagedSettings()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            LiveKitConfigurationYaml.SerializeConfiguration(
                new LiveKitIngressUpstreamConfiguration { ApiKey = "user-key" },
                "configuration",
                jsonLoggingEnabled: false,
                additionalYaml: null,
                "api_key"));

        Assert.Contains("managed by the typed LiveKit resource configuration", exception.Message);
    }

    [Fact]
    public void SerializeConfiguration_RejectsDuplicateTypedAndAdditionalTopLevelSettings()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            LiveKitConfigurationYaml.SerializeConfiguration(
                new LiveKitIngressUpstreamConfiguration
                {
                    CpuCost = new CPUCostConfig { RtmpCpuCost = 2 },
                },
                "configuration",
                jsonLoggingEnabled: false,
                additionalYaml: "cpu_cost:\n  whip_cpu_cost: 1"));

        Assert.Contains("configured both", exception.Message);
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

    [Fact]
    public void ValidateAdditionalConfiguration_RejectsNonScalarTopLevelKeys()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            LiveKitConfigurationYaml.ValidateAdditionalConfiguration("? [one, two]\n: value", "yaml"));

        Assert.Contains("Top-level YAML keys must be scalar", exception.Message);
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

}
