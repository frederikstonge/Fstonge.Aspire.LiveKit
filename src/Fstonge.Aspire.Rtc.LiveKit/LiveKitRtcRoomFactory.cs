using LiveKit.Rtc;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Rtc.LiveKit;

public sealed class LiveKitRtcRoomFactory : ILiveKitRtcRoomFactory
{
    public LiveKitRtcRoomFactory(IOptions<LiveKitRtcOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ServerUrl = GetWebSocketUrl(options.Value.Url);
    }

    public Uri ServerUrl { get; }

    public async Task<Room> ConnectAsync(string participantToken, RoomOptions? roomOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantToken);

        var room = new Room();
        try
        {
            await room.ConnectAsync(ServerUrl.AbsoluteUri, participantToken, roomOptions);
            return room;
        }
        catch
        {
            room.Dispose();
            throw;
        }
    }

    internal static bool IsValidServerUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp
            || uri.Scheme == Uri.UriSchemeHttps
            || uri.Scheme == "ws"
            || uri.Scheme == "wss";
    }

    private static Uri GetWebSocketUrl(string? value)
    {
        if (!IsValidServerUrl(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new OptionsValidationException(
                nameof(LiveKitRtcOptions),
                typeof(LiveKitRtcOptions),
                ["LIVEKIT_URL must be an absolute HTTP, HTTPS, WS, or WSS URL without credentials, query, or fragment."]);
        }

        var scheme = uri.Scheme == Uri.UriSchemeHttp
            ? "ws"
            : uri.Scheme == Uri.UriSchemeHttps
                ? "wss"
                : uri.Scheme;

        return new UriBuilder(uri) { Scheme = scheme }.Uri;
    }
}
