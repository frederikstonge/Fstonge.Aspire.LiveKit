using System.Text.Json;

namespace Aspire.Hosting;

internal interface IJsonLogParser
{
    bool TryParse(
        JsonElement root,
        string rawContent,
        int lineNumber,
        bool isErrorMessage,
        DateTimeOffset? resourceTimestamp,
        out ForwardedLogRecord record);
}
