using System.Diagnostics.CodeAnalysis;
using LiveKit.Rtc;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Rtc.LiveKit;

/// <summary>
/// Default implementation of <see cref="ILiveKitRtcRoomFactory"/> that validates options and connects to LiveKit rooms.
/// </summary>
public sealed class LiveKitRtcRoomFactory : ILiveKitRtcRoomFactory
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveKitRtcRoomFactory"/> class using the specified options.
    /// </summary>
    /// <param name="options">The configured LiveKit RTC options.</param>
    public LiveKitRtcRoomFactory(IOptions<LiveKitRtcOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ServerUrl = GetWebSocketUrl(options.Value.WebSocketUrl);
    }

    /// <inheritdoc />
    public Uri ServerUrl { get; }

    /// <inheritdoc />
    // The successful connection path requires a running LiveKit server.
    [ExcludeFromCodeCoverage]
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

        return uri.Scheme == "ws"
            || uri.Scheme == "wss";
    }

    private static Uri GetWebSocketUrl(string? value)
    {
        if (!IsValidServerUrl(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new OptionsValidationException(
                nameof(LiveKitRtcOptions),
                typeof(LiveKitRtcOptions),
                ["LIVEKIT_WS_URL must be an absolute WS or WSS URL without credentials, query, or fragment."]);
        }

        return uri;
    }
}
