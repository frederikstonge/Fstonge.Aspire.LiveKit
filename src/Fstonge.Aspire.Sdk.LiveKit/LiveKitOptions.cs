namespace Fstonge.Aspire.Sdk.LiveKit;

public sealed class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    public string? Url { get; set; }

    public string? ApiKey { get; set; }

    public string? ApiSecret { get; set; }
}
