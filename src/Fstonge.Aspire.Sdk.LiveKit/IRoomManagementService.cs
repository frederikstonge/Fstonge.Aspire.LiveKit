using Livekit.Server.Sdk.Dotnet;

namespace Fstonge.Aspire.Sdk.LiveKit;

public interface IRoomManagementService
{
    Task<Room> CreateRoomAsync(CreateRoomRequest request);
    Task<DeleteRoomResponse> DeleteRoomAsync(DeleteRoomRequest request);
    Task<ForwardParticipantResponse> ForwardParticipantAsync(ForwardParticipantRequest request);
    Task<ParticipantInfo> GetParticipantAsync(RoomParticipantIdentity request);
    Task<ListParticipantsResponse> ListParticipantsAsync(ListParticipantsRequest request);
    Task<ListRoomsResponse> ListRoomsAsync(ListRoomsRequest request);
    Task<MoveParticipantResponse> MoveParticipantAsync(MoveParticipantRequest request);
    Task<MuteRoomTrackResponse> MutePublishedTrackAsync(MuteRoomTrackRequest request);
    Task<RemoveParticipantResponse> RemoveParticipantAsync(RoomParticipantIdentity request);
    Task<SendDataResponse> SendDataAsync(SendDataRequest request);
    Task<ParticipantInfo> UpdateParticipantAsync(UpdateParticipantRequest request);
    Task<Room> UpdateRoomMetadataAsync(UpdateRoomMetadataRequest request);
    Task<UpdateSubscriptionsResponse> UpdateSubscriptionsAsync(UpdateSubscriptionsRequest request);
}