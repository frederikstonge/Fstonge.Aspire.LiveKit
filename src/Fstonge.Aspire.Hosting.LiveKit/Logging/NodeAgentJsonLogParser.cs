using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

internal sealed class NodeAgentJsonLogParser : JsonLogParser
{
    private static readonly string[] MessageFields = ["msg", "message"];
    private static readonly string[] LevelFields = ["level"];
    private static readonly string[] TimestampFields = ["time"];

    protected override IReadOnlyList<string> MessageProperties => MessageFields;
    protected override IReadOnlyList<string> LevelProperties => LevelFields;
    protected override IReadOnlyList<string> TimestampProperties => TimestampFields;

    protected override bool Matches(JsonElement root) =>
        HasAnyProperty(root, MessageFields)
        && HasNumericProperty(root, "level")
        && HasNumericProperty(root, "time");

    protected override LogLevel ParseLogLevel(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var numericLevel))
        {
            return base.ParseLogLevel(value);
        }

        return numericLevel switch
        {
            <= 10 => LogLevel.Trace,
            <= 20 => LogLevel.Debug,
            <= 30 => LogLevel.Information,
            <= 40 => LogLevel.Warning,
            <= 50 => LogLevel.Error,
            _ => LogLevel.Critical
        };
    }

    private static bool HasNumericProperty(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number;
}
