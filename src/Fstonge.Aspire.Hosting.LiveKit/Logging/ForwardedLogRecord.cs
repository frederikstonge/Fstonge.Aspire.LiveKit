using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

internal sealed record ForwardedLogRecord(
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Properties);
