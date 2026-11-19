namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Provides connection details for managed and external LiveKit server resources.
/// </summary>
public interface ILiveKitServerResource
{
    /// <summary>
    /// Gets the LiveKit HTTP API URL.
    /// </summary>
    ReferenceExpression Url { get; }

    /// <summary>
    /// Gets the LiveKit WebSocket URL.
    /// </summary>
    ReferenceExpression WebSocketUrl { get; }

    /// <summary>
    /// Gets the optional API key parameter.
    /// </summary>
    IResourceBuilder<ParameterResource>? ApiKey { get; }

    /// <summary>
    /// Gets the optional API secret parameter.
    /// </summary>
    IResourceBuilder<ParameterResource>? ApiSecret { get; }
}
