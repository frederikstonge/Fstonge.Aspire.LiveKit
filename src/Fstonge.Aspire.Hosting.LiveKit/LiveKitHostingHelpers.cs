using System.Globalization;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

public static class LiveKitHostingHelpers
{
    internal static void ValidateOptionalPort(int? port, string parameterName)
    {
        if (port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Port numbers must be between 1 and 65535.");
        }
    }

    internal static string GetJsonLoggingConfiguration(LiveKitContainerResource resource) =>
        resource.JsonLoggingEnabled ? "logging:\n  json: true\n" : string.Empty;

    internal static ReferenceExpression GetHttpPortConfiguration(int? port, string key) =>
        port is null
            ? ReferenceExpression.Empty
            : ReferenceExpression.Create($"{key}: {port.Value.ToString(CultureInfo.InvariantCulture)}\n");
}