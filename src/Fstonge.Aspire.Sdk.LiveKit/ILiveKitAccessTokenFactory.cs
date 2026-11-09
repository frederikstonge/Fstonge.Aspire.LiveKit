using Livekit.Server.Sdk.Dotnet;

namespace Fstonge.Aspire.Sdk.LiveKit;

/// <summary>
/// Factory for creating pre-authenticated LiveKit access tokens.
/// </summary>
public interface IAccessTokenFactory
{
    /// <summary>
    /// Creates a new <see cref="AccessToken"/> configured with the API key and secret.
    /// </summary>
    /// <returns>A new <see cref="AccessToken"/> instance.</returns>
    AccessToken GetAccessToken();
}
