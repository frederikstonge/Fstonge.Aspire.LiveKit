using Aspire.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Egress;

public sealed class LiveKitEgressConfigurationTests
{
    [Fact]
    public void ToYaml_EmitsConfiguredSettingsAndNormalizesLogLevel()
    {
        var configuration = new LiveKitEgressConfiguration
        {
            TemplatePort = 7981,
            DebugHandlerPort = 8080,
            PulseSinkReapGraceSeconds = -1,
            TemplateBase = "https://templates.example.test/",
            EnableChromeSandbox = false,
            Logging = new LiveKitServiceLoggingConfiguration { Level = "WARN" },
        };

        var yaml = configuration.ToYaml(jsonLoggingEnabled: true);

        Assert.Contains("template_port: 7981", yaml);
        Assert.Contains("debug_handler_port: 8080", yaml);
        Assert.Contains("pulse_sink_reap_grace_sec: -1", yaml);
        Assert.Contains("template_base: https://templates.example.test/", yaml);
        Assert.Contains("enable_chrome_sandbox: false", yaml);
        Assert.Contains("level: warn", yaml);
        Assert.Contains("json: true", yaml);
    }

    [Fact]
    public void ToYaml_RejectsConfigurationWithoutSettings()
    {
        Assert.Throws<InvalidOperationException>(() => new LiveKitEgressConfiguration().ToYaml(false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void ToYaml_RejectsOutOfRangePorts(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LiveKitEgressConfiguration { TemplatePort = port }.ToYaml(false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LiveKitEgressConfiguration { DebugHandlerPort = port }.ToYaml(false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ToYaml_RejectsBlankTemplateBase(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            new LiveKitEgressConfiguration { TemplateBase = value }.ToYaml(false));
    }

    [Fact]
    public void ToYaml_RejectsUnsupportedLogLevel()
    {
        var configuration = new LiveKitEgressConfiguration
        {
            Logging = new LiveKitServiceLoggingConfiguration { Level = "trace" },
        };

        Assert.Throws<ArgumentException>(() => configuration.ToYaml(false));
    }
}
