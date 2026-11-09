using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

internal static class LiveKitTabDelimitedLogParser
{
    public static bool TryParse(
        string content,
        int lineNumber,
        DateTimeOffset? resourceTimestamp,
        out ForwardedLogRecord record)
    {
        var fields = content.Split('\t', 6, StringSplitOptions.None);
        if (fields.Length < 5
            || !DateTimeOffset.TryParse(
                fields[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var timestamp)
            || JsonLogParser.GetLogLevel(fields[1]) is not { } logLevel)
        {
            record = default!;
            return false;
        }

        JsonDocument? attributesDocument = null;
        if (fields.Length == 6)
        {
            try
            {
                attributesDocument = JsonDocument.Parse(fields[5]);
            }
            catch (JsonException)
            {
                record = default!;
                return false;
            }

            if (attributesDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                attributesDocument.Dispose();
                record = default!;
                return false;
            }
        }

        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["livekit.log.line_number"] = lineNumber,
            ["livekit.logger"] = fields[2],
            ["livekit.caller"] = fields[3],
            ["livekit.timestamp"] = timestamp
        };
        if (resourceTimestamp is { } aspireTimestamp)
        {
            properties["aspire.timestamp"] = aspireTimestamp;
        }

        using (attributesDocument)
        {
            if (attributesDocument is not null)
            {
                foreach (var property in attributesDocument.RootElement.EnumerateObject())
                {
                    properties[property.Name] = JsonLogParser.GetPropertyValue(property.Value);
                }
            }
        }

        record = new ForwardedLogRecord(logLevel, fields[4], properties);
        return true;
    }
}
