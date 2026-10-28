using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Sdk.LiveKit;

public class RoomManagementService : IRoomManagementService
{
    private readonly IOptions<LiveKitOptions> _options;
    private readonly RoomServiceClient _roomServiceClient;

    public RoomManagementService(HttpClient httpClient, IOptions<LiveKitOptions> options)
    {
        _options = options;
        _roomServiceClient = new RoomServiceClient(_options.Value.Url, _options.Value.ApiKey, _options.Value.ApiSecret, httpClient);
    }

    // Rooms
    public Task<Room> CreateRoomAsync(CreateRoomRequest request) => _roomServiceClient.CreateRoom(request);

    public Task<ListRoomsResponse> ListRoomsAsync(ListRoomsRequest request) => _roomServiceClient.ListRooms(request);

    public Task<DeleteRoomResponse> DeleteRoomAsync(DeleteRoomRequest request) => _roomServiceClient.DeleteRoom(request);

    public Task<Room> UpdateRoomMetadataAsync(UpdateRoomMetadataRequest request) => _roomServiceClient.UpdateRoomMetadata(request);

    // Participants
    public Task<ParticipantInfo> GetParticipantAsync(RoomParticipantIdentity request) => _roomServiceClient.GetParticipant(request);

    public Task<ListParticipantsResponse> ListParticipantsAsync(ListParticipantsRequest request) => _roomServiceClient.ListParticipants(request);

    public Task<MoveParticipantResponse> MoveParticipantAsync(MoveParticipantRequest request) => _roomServiceClient.MoveParticipant(request);

    public Task<RemoveParticipantResponse> RemoveParticipantAsync(RoomParticipantIdentity request) => _roomServiceClient.RemoveParticipant(request);

    public Task<ParticipantInfo> UpdateParticipantAsync(UpdateParticipantRequest request) => _roomServiceClient.UpdateParticipant(request);

    public Task<ForwardParticipantResponse> ForwardParticipantAsync(ForwardParticipantRequest request) => _roomServiceClient.ForwardParticipant(request);

    // Others
    public Task<UpdateSubscriptionsResponse> UpdateSubscriptionsAsync(UpdateSubscriptionsRequest request) => _roomServiceClient.UpdateSubscriptions(request);
    public Task<MuteRoomTrackResponse> MutePublishedTrackAsync(MuteRoomTrackRequest request) => _roomServiceClient.MutePublishedTrack(request);
    public Task<SendDataResponse> SendDataAsync(SendDataRequest request) => _roomServiceClient.SendData(request);
}