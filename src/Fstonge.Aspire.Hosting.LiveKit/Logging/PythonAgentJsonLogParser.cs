using System.Text.Json;

namespace Aspire.Hosting;

internal sealed class PythonAgentJsonLogParser : JsonLogParser
{
    private static readonly string[] MessageFields = ["message", "msg"];
    private static readonly string[] LevelFields = ["level", "levelname", "severity"];
    private static readonly string[] TimestampFields = ["timestamp", "asctime", "time", "created"];
    private static readonly string[] PythonAgentFields = ["message", "levelname"];

    protected override IReadOnlyList<string> MessageProperties => MessageFields;
    protected override IReadOnlyList<string> LevelProperties => LevelFields;
    protected override IReadOnlyList<string> TimestampProperties => TimestampFields;

    protected override bool Matches(JsonElement root) => HasAnyProperty(root, PythonAgentFields);
}
