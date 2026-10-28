using Fstonge.Aspire.Hosting.LiveKit;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

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
        Assert.Contains("logging:", yaml);
        Assert.Contains("level: warn", yaml);
        Assert.Contains("json: true", yaml);
    }

    [Fact]
    public void ToYaml_EmitsSettingsWithoutUnconfiguredDefaults()
    {
        var configuration = new LiveKitEgressConfiguration { TemplatePort = 65535 };

        var yaml = configuration.ToYaml(jsonLoggingEnabled: false);

        Assert.Contains("template_port: 65535", yaml);
        Assert.DoesNotContain("debug_handler_port", yaml);
        Assert.DoesNotContain("pulse_sink_reap_grace_sec", yaml);
        Assert.DoesNotContain("template_base", yaml);
        Assert.DoesNotContain("enable_chrome_sandbox", yaml);
        Assert.DoesNotContain("logging", yaml);
    }

    [Fact]
    public void ToYaml_RejectsConfigurationWithoutSettings()
    {
        var configuration = new LiveKitEgressConfiguration();

        var exception = Assert.Throws<InvalidOperationException>(
            () => configuration.ToYaml(jsonLoggingEnabled: false));

        Assert.Contains("At least one Egress configuration setting", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void ToYaml_RejectsInvalidTemplatePort(int port)
    {
        var configuration = new LiveKitEgressConfiguration { TemplatePort = port };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => configuration.ToYaml(jsonLoggingEnabled: false));

        Assert.Equal("template_port", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void ToYaml_RejectsInvalidDebugHandlerPort(int port)
    {
        var configuration = new LiveKitEgressConfiguration { DebugHandlerPort = port };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => configuration.ToYaml(jsonLoggingEnabled: false));

        Assert.Equal("debug_handler_port", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ToYaml_RejectsBlankTemplateBase(string? templateBase)
    {
        var configuration = new LiveKitEgressConfiguration { TemplateBase = templateBase };

        Assert.Throws<ArgumentException>(() => configuration.ToYaml(jsonLoggingEnabled: false));
    }

    [Fact]
    public void ToYaml_RejectsUnsupportedLogLevel()
    {
        var configuration = new LiveKitEgressConfiguration
        {
            Logging = new LiveKitServiceLoggingConfiguration { Level = "trace" },
        };

        Assert.Throws<ArgumentException>(() => configuration.ToYaml(jsonLoggingEnabled: false));
    }
}
