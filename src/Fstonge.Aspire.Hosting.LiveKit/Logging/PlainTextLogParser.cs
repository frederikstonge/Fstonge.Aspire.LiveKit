using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

internal static class PlainTextLogParser
{
    public static ForwardedLogRecord Parse(
        string content,
        bool isErrorMessage,
        int lineNumber,
        DateTimeOffset? timestamp)
    {
        var properties = new Dictionary<string, object?>
        {
            ["livekit.log.line_number"] = lineNumber,
            ["aspire.log.is_error_stream"] = isErrorMessage
        };
        if (timestamp is { } logTimestamp)
        {
            properties["livekit.timestamp"] = logTimestamp;
        }

        return new ForwardedLogRecord(GetLogLevel(content), content, properties);
    }

    private static LogLevel GetLogLevel(string content)
    {
        var prefix = content.AsSpan().TrimStart();
        if (prefix.Contains('\t'))
        {
            var tabFields = prefix.ToString().Split('\t', 5, StringSplitOptions.None);
            foreach (var field in tabFields)
            {
                if (JsonLogParser.GetLogLevel(field) is { } tabLogLevel)
                {
                    return tabLogLevel;
                }
            }
        }

        if (prefix.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracket = prefix.IndexOf(']');
            if (closingBracket > 1)
            {
                prefix = prefix[1..closingBracket];
            }
        }
        else
        {
            var separator = prefix.IndexOfAny(' ', '\t', ':');
            if (separator > 0)
            {
                prefix = prefix[..separator];
            }
        }

        return JsonLogParser.GetLogLevel(prefix.ToString()) ?? LogLevel.Information;
    }
}
