namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a LiveKit server that is managed outside the Aspire application.
/// </summary>
[AspireExport]
public sealed class ExternalLiveKitServerResource : Resource, ILiveKitServerResource
{
    internal ExternalLiveKitServerResource(
        string name,
        ReferenceExpression url,
        IResourceBuilder<ParameterResource>? apiKey,
        IResourceBuilder<ParameterResource>? apiSecret)
        : base(name)
    {
        Url = url;
        ApiKey = apiKey;
        ApiSecret = apiSecret;
    }

    public ReferenceExpression Url { get; }

    public IResourceBuilder<ParameterResource>? ApiKey { get; }

    public IResourceBuilder<ParameterResource>? ApiSecret { get; }
}
