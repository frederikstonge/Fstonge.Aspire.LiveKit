namespace Fstonge.Aspire.Hosting.LiveKit;

public sealed class LiveKitEgressConfiguration
{
    public int? TemplatePort { get; init; }

    public int? DebugHandlerPort { get; init; }

    public int? PulseSinkReapGraceSeconds { get; init; }

    public string? TemplateBase { get; init; }

    public bool? EnableChromeSandbox { get; init; }

    public LiveKitServiceLoggingConfiguration? Logging { get; init; }

    internal string ToYaml(bool jsonLoggingEnabled)
    {
        var yaml = new Dictionary<string, object?>();
        AddPort(yaml, "template_port", TemplatePort);
        AddPort(yaml, "debug_handler_port", DebugHandlerPort);
        if (PulseSinkReapGraceSeconds is { } graceSeconds)
        {
            yaml["pulse_sink_reap_grace_sec"] = graceSeconds;
        }

        if (TemplateBase is { } templateBase)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(templateBase, nameof(TemplateBase));
            yaml["template_base"] = templateBase;
        }

        if (EnableChromeSandbox is { } enableChromeSandbox)
        {
            yaml["enable_chrome_sandbox"] = enableChromeSandbox;
        }

        var logging = LiveKitConfigurationYaml.CreateLogging(Logging, jsonLoggingEnabled);
        if (logging is not null)
        {
            yaml["logging"] = logging;
        }

        if (yaml.Count == 0)
        {
            throw new InvalidOperationException("At least one Egress configuration setting must be provided.");
        }

        return LiveKitConfigurationYaml.Serialize(yaml);
    }

    private static void AddPort(IDictionary<string, object?> yaml, string name, int? port)
    {
        if (port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(name, "Port numbers must be between 1 and 65535.");
        }

        if (port is { } value)
        {
            yaml[name] = value;
        }
    }
}
