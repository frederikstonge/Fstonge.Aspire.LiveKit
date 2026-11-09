using System.Text.Json;

namespace Aspire.Hosting;

internal sealed class LiveKitServerJsonLogParser : JsonLogParser
{
    private static readonly string[] MessageFields = ["msg"];
    private static readonly string[] LevelFields = ["level"];
    private static readonly string[] TimestampFields = ["time"];

    protected override IReadOnlyList<string> MessageProperties => MessageFields;
    protected override IReadOnlyList<string> LevelProperties => LevelFields;
    protected override IReadOnlyList<string> TimestampProperties => TimestampFields;

    protected override bool Matches(JsonElement root) =>
        HasAnyProperty(root, MessageFields)
        && (HasAnyProperty(root, LevelFields) || HasAnyProperty(root, TimestampFields));
}
