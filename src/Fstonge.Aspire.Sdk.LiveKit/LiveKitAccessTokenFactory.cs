using System.Security.Claims;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Sdk.LiveKit;

/// <summary>
/// Default implementation of <see cref="IAccessTokenFactory"/> that creates access tokens using configured credentials.
/// </summary>
public class LiveKitAccessTokenFactory : IAccessTokenFactory
{
    private readonly LiveKitOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveKitAccessTokenFactory"/> class with the specified options.
    /// </summary>
    /// <param name="options">The configured LiveKit options.</param>
    public LiveKitAccessTokenFactory(IOptions<LiveKitOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public AccessToken GetAccessToken()
    {            
        return new AccessToken(_options.ApiKey, _options.ApiSecret);
    }
}
