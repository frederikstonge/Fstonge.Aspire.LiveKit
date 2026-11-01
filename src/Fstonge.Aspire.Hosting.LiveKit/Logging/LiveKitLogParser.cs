using System.Globalization;
using System.Text.Json;

namespace Aspire.Hosting;

internal sealed class LiveKitLogParser
{
    private readonly IJsonLogParser[] jsonParsers =
    [
        new NodeAgentJsonLogParser(),
        new PythonAgentJsonLogParser(),
        new LiveKitServerJsonLogParser(),
        new GenericJsonLogParser()
    ];

    public ForwardedLogRecord Parse(string content, bool isErrorMessage, int lineNumber)
    {
        DateTimeOffset? resourceTimestamp = null;
        var timestampEnd = content.IndexOf(' ');
        if (timestampEnd > 0
            && DateTimeOffset.TryParse(
                content.AsSpan(0, timestampEnd),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsedTimestamp))
        {
            resourceTimestamp = parsedTimestamp;
            content = content[(timestampEnd + 1)..];
        }

        if (LiveKitTabDelimitedLogParser.TryParse(
            content,
            lineNumber,
            resourceTimestamp,
            out var tabDelimitedRecord))
        {
            return tabDelimitedRecord;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var parser in jsonParsers)
                {
                    if (parser.TryParse(
                        document.RootElement,
                        content,
                        lineNumber,
                        isErrorMessage,
                        resourceTimestamp,
                        out var record))
                    {
                        return record;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return PlainTextLogParser.Parse(content, isErrorMessage, lineNumber, resourceTimestamp);
    }
}
