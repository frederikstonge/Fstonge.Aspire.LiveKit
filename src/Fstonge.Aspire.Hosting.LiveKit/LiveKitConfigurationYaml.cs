using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Fstonge.Aspire.Hosting.LiveKit;

internal static class LiveKitConfigurationYaml
{
    private static readonly ISerializer Serializer = new SerializerBuilder().Build();

    public static string Serialize(IDictionary<string, object?> configuration) =>
        Serializer.Serialize(configuration);

    public static string SerializeScalar(string value) =>
        Serializer.Serialize(value).TrimEnd('\r', '\n');

    public static IDictionary<string, object?>? CreateLogging(
        LiveKitServiceLoggingConfiguration logging,
        bool jsonLoggingEnabled)
    {
        var settings = new Dictionary<string, object?>();
        if (logging.Level is { } level)
        {
            settings["level"] = ValidateLogLevel(level, nameof(logging.Level));
        }

        if (jsonLoggingEnabled)
        {
            settings["json"] = true;
        }

        return settings.Count == 0 ? null : settings;
    }

    public static string ValidateAdditionalConfiguration(
        string yaml,
        string parameterName,
        params string[] reservedKeys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml, parameterName);

        var yamlStream = new YamlStream();
        try
        {
            yamlStream.Load(new StringReader(yaml));
        }
        catch (YamlException exception)
        {
            throw new ArgumentException("The additional configuration must be valid YAML.", parameterName, exception);
        }

        if (yamlStream.Documents.Count != 1
            || yamlStream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new ArgumentException("The additional configuration must contain one top-level YAML mapping.", parameterName);
        }

        var reserved = reservedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (keyNode, _) in root.Children)
        {
            if (keyNode is not YamlScalarNode { Value: { } key })
            {
                throw new ArgumentException("Top-level YAML keys must be scalar values.", parameterName);
            }

            if (!seenKeys.Add(key))
            {
                throw new ArgumentException($"The top-level key '{key}' is specified more than once.", parameterName);
            }

            if (reserved.Contains(key))
            {
                throw new ArgumentException(
                    $"The '{key}' setting is managed by the typed LiveKit resource configuration.",
                    parameterName);
            }
        }

        return yaml.EndsWith('\n') ? yaml : yaml + "\n";
    }

    public static string ValidateLogLevel(string? level, string parameterName)
    {
        if (level is null)
        {
            return string.Empty;
        }

        var normalizedLevel = level.ToLowerInvariant();
        if (normalizedLevel is not ("debug" or "info" or "warn" or "error"))
        {
            throw new ArgumentException(
                "Log level must be debug, info, warn, or error.",
                parameterName);
        }

        return normalizedLevel;
    }

}

public sealed class LiveKitServiceLoggingConfiguration
{
    /// <summary>
    /// Minimum log severity to emit. Accepted values are debug, info, warn, and error.
    /// Default: <c>info</c> for the companion services.
    /// </summary>
    public string? Level { get; set; }
}
