using LiveKit.Rtc;

namespace Fstonge.Aspire.Rtc.LiveKit;

/// <summary>
/// Factory for creating and connecting to LiveKit RTC rooms.
/// </summary>
public interface ILiveKitRtcRoomFactory
{
    /// <summary>
    /// Gets the resolved WebSocket server URL used for connecting to LiveKit.
    /// </summary>
    Uri ServerUrl { get; }

    /// <summary>
    /// Connects to a LiveKit room using the provided participant token and optional room configuration options.
    /// </summary>
    /// <param name="participantToken">The JWT participant access token.</param>
    /// <param name="roomOptions">Optional client room options for audio, video, and connection settings.</param>
    /// <returns>A connected <see cref="Room"/> instance.</returns>
    Task<Room> ConnectAsync(string participantToken, RoomOptions? roomOptions = null);
}
