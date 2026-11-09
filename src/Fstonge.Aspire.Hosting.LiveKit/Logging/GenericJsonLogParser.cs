namespace Aspire.Hosting;

internal sealed class GenericJsonLogParser : JsonLogParser
{
    private static readonly string[] MessageFields = ["message", "msg", "body"];
    private static readonly string[] LevelFields = ["level", "levelname", "severity"];
    private static readonly string[] TimestampFields = ["timestamp", "asctime", "time", "created"];

    protected override IReadOnlyList<string> MessageProperties => MessageFields;
    protected override IReadOnlyList<string> LevelProperties => LevelFields;
    protected override IReadOnlyList<string> TimestampProperties => TimestampFields;
}
