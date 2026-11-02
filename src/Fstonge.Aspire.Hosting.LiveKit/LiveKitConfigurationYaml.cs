using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Aspire.Hosting;

internal static class LiveKitConfigurationYaml
{
    private static readonly ISerializer Serializer = new SerializerBuilder().Build();
    private static readonly ISerializer ConfigurationSerializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();
    private static readonly IDeserializer ConfigurationDeserializer = new DeserializerBuilder().Build();

    public static string Serialize(YamlMappingNode configuration)
    {
        var yamlStream = new YamlStream(new YamlDocument(configuration));
        using var writer = new StringWriter();
        yamlStream.Save(writer, assignAnchors: false);
        var lines = writer.ToString().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
        if (lines.Count > 0 && lines[0] == "---")
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count > 0 && lines[^1] == "...")
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines) + "\n";
    }

    public static string SerializeScalar(string value) =>
        Serializer.Serialize(value).TrimEnd('\r', '\n');

    public static T CloneConfiguration<T>(T configuration)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var yaml = ConfigurationSerializer.Serialize(configuration);
        return ConfigurationDeserializer.Deserialize<T>(yaml)
            ?? throw new InvalidOperationException($"Could not clone configuration type '{typeof(T).Name}'.");
    }

    public static string SerializeConfiguration(
        object configuration,
        string parameterName,
        bool jsonLoggingEnabled,
        string? additionalYaml,
        params string[] reservedKeys)
    {
        var root = CreateConfigurationNode(configuration, parameterName, reservedKeys);
        if (root.Children.Count == 0)
        {
            throw new InvalidOperationException("At least one configuration setting must be provided.");
        }

        ValidateNoDuplicateTopLevelSettings(root, additionalYaml, parameterName);
        if (jsonLoggingEnabled)
        {
            EnableJsonLogging(root, parameterName);
        }

        return Serialize(root);
    }

    public static YamlMappingNode CreateConfigurationNode(
        object configuration,
        string parameterName,
        params string[] reservedKeys)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var yaml = ConfigurationSerializer.Serialize(configuration);
        var yamlStream = new YamlStream();
        try
        {
            yamlStream.Load(new StringReader(yaml));
        }
        catch (YamlException exception)
        {
            throw new ArgumentException("The generated configuration must be valid YAML.", parameterName, exception);
        }

        if (yamlStream.Documents.Count != 1
            || yamlStream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new ArgumentException("The generated configuration must contain one top-level YAML mapping.", parameterName);
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

        return root;
    }

    public static void EnableJsonLogging(YamlMappingNode configuration, string parameterName)
    {
        var loggingKey = new YamlScalarNode("logging");
        if (configuration.Children.TryGetValue(loggingKey, out var loggingNode))
        {
            if (loggingNode is not YamlMappingNode logging)
            {
                throw new ArgumentException("The logging configuration must be a YAML mapping.", parameterName);
            }

            logging.Children[new YamlScalarNode("json")] = new YamlScalarNode("true");
        }
        else
        {
            configuration.Children[loggingKey] = new YamlMappingNode
            {
                { new YamlScalarNode("json"), new YamlScalarNode("true") },
            };
        }
    }

    public static void ValidateNoDuplicateTopLevelSettings(
        YamlMappingNode configuration,
        string? additionalYaml,
        string parameterName)
    {
        if (additionalYaml is null)
        {
            return;
        }

        var additional = new YamlStream();
        try
        {
            additional.Load(new StringReader(additionalYaml));
        }
        catch (YamlException exception)
        {
            throw new ArgumentException("The additional configuration must be valid YAML.", parameterName, exception);
        }

        if (additional.Documents.Count != 1
            || additional.Documents[0].RootNode is not YamlMappingNode additionalRoot)
        {
            throw new ArgumentException("The additional configuration must contain one top-level YAML mapping.", parameterName);
        }

        var configuredKeys = configuration.Children.Keys
            .OfType<YamlScalarNode>()
            .Select(key => key.Value)
            .Where(key => key is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var keyNode in additionalRoot.Children.Keys)
        {
            if (keyNode is YamlScalarNode { Value: { } key } && configuredKeys.Contains(key))
            {
                throw new ArgumentException(
                    $"The top-level key '{key}' is configured both in the typed and additional configuration.",
                    parameterName);
            }
        }
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

}
