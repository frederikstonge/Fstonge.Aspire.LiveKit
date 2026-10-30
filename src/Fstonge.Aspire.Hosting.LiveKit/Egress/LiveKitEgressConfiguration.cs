namespace Aspire.Hosting;

/// <summary>
/// Represents configuration options for the LiveKit Egress service.
/// </summary>
public sealed class LiveKitEgressConfiguration
{
    /// <summary>
    /// Gets or sets the port for Egress's room-composite template server. Default: <c>7980</c>.
    /// </summary>
    public int? TemplatePort { get; set; }

    /// <summary>
    /// Gets or sets the port for Egress debug HTTP handlers. Default: <c>0</c> (disabled).
    /// </summary>
    public int? DebugHandlerPort { get; set; }

    /// <summary>
    /// Gets or sets the grace period in seconds before reaping an orphaned PulseAudio sink. Default: <c>30</c> seconds.
    /// A negative value disables reaping.
    /// </summary>
    public int? PulseSinkReapGraceSeconds { get; set; }

    /// <summary>
    /// Gets or sets the base URL for custom Egress templates. Default: <c>http://localhost:&lt;template_port&gt;/</c>.
    /// </summary>
    public string? TemplateBase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Chrome runs with sandboxing enabled. Default: <see langword="false"/>.
    /// </summary>
    public bool? EnableChromeSandbox { get; set; }

    /// <summary>
    /// Gets or sets the configuration for Egress log verbosity and JSON output. Default log level: <c>info</c>; JSON output is disabled
    /// unless enabled by <c>WithJsonLogging</c> or <c>WithStructuredLogs</c>.
    /// </summary>
    public LiveKitServiceLoggingConfiguration Logging { get; set; } = new();

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
