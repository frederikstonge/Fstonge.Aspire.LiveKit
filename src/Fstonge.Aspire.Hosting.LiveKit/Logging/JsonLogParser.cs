using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

internal abstract class JsonLogParser : IJsonLogParser
{
    protected abstract IReadOnlyList<string> MessageProperties { get; }
    protected abstract IReadOnlyList<string> LevelProperties { get; }
    protected abstract IReadOnlyList<string> TimestampProperties { get; }

    protected virtual bool Matches(JsonElement root) => true;

    public bool TryParse(
        JsonElement root,
        string rawContent,
        int lineNumber,
        bool isErrorMessage,
        DateTimeOffset? resourceTimestamp,
        out ForwardedLogRecord record)
    {
        if (root.ValueKind != JsonValueKind.Object || !Matches(root))
        {
            record = default!;
            return false;
        }

        var properties = CreateBaseProperties(lineNumber, isErrorMessage);
        foreach (var property in root.EnumerateObject())
        {
            if (!IsKnownField(property.Name))
            {
                properties[property.Name] = GetPropertyValue(property.Value);
            }
        }

        if (TryGetProperty(root, TimestampProperties, out var timestamp))
        {
            properties["livekit.timestamp"] = GetPropertyValue(timestamp);
        }
        else if (resourceTimestamp is { } parsedTimestamp)
        {
            properties["livekit.timestamp"] = parsedTimestamp;
        }

        var message = TryGetProperty(root, MessageProperties, out var messageProperty)
            && messageProperty.ValueKind == JsonValueKind.String
                ? messageProperty.GetString()!
                : rawContent;
        var level = TryGetProperty(root, LevelProperties, out var levelProperty)
            ? ParseLogLevel(levelProperty)
            : LogLevel.Information;

        record = new ForwardedLogRecord(level, message, properties);
        return true;
    }

    private bool IsKnownField(string name) =>
        ContainsField(MessageProperties, name)
        || ContainsField(LevelProperties, name)
        || ContainsField(TimestampProperties, name);

    private static bool ContainsField(IReadOnlyList<string> fields, string name) =>
        fields.Any(field => string.Equals(field, name, StringComparison.OrdinalIgnoreCase));

    protected static bool HasAnyProperty(JsonElement root, IReadOnlyList<string> names) =>
        TryGetProperty(root, names, out _);

    private static bool TryGetProperty(JsonElement root, IReadOnlyList<string> names, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (names.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static Dictionary<string, object?> CreateBaseProperties(int lineNumber, bool isErrorMessage)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["livekit.log.line_number"] = lineNumber
        };
        if (isErrorMessage)
        {
            properties["aspire.log.is_error_stream"] = true;
        }

        return properties;
    }

    protected virtual LogLevel ParseLogLevel(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return GetLogLevel(value.GetString()) ?? LogLevel.Information;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numericLevel))
        {
            return numericLevel switch
            {
                <= 0 => LogLevel.Trace,
                <= 10 => LogLevel.Debug,
                <= 20 => LogLevel.Information,
                <= 30 => LogLevel.Warning,
                <= 40 => LogLevel.Error,
                _ => LogLevel.Critical
            };
        }

        return LogLevel.Information;
    }

    internal static LogLevel? GetLogLevel(string? level) =>
        level?.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" or "err" => LogLevel.Error,
            "fatal" or "panic" or "critical" => LogLevel.Critical,
            _ => null
        };

    internal static object? GetPropertyValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
}
