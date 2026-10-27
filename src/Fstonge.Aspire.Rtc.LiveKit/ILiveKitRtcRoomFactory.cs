using LiveKit.Rtc;

namespace Fstonge.Aspire.Rtc.LiveKit;

public interface ILiveKitRtcRoomFactory
{
    Uri ServerUrl { get; }

    Task<Room> ConnectAsync(string participantToken, RoomOptions? roomOptions = null);
}
