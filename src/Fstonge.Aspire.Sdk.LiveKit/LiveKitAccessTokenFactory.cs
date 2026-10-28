using System.Security.Claims;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Sdk.LiveKit;

public class LiveKitAccessTokenFactory : IAccessTokenFactory
{
    private readonly LiveKitOptions _options;

    public LiveKitAccessTokenFactory(IOptions<LiveKitOptions> options)
    {
        _options = options.Value;
    }

    public AccessToken GetAccessToken()
    {            
        return new AccessToken(_options.ApiKey, _options.ApiSecret);
    }
}