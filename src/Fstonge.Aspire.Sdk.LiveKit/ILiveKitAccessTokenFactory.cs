using Livekit.Server.Sdk.Dotnet;

namespace Fstonge.Aspire.Sdk.LiveKit;

public interface IAccessTokenFactory
{
    AccessToken GetAccessToken();
}