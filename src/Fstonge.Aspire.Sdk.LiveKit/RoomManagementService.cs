using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Sdk.LiveKit;

/// <summary>
/// Default implementation of <see cref="IRoomManagementService"/> using the underlying LiveKit <see cref="RoomServiceClient"/>.
/// </summary>
public class RoomManagementService : IRoomManagementService
{
    private readonly IOptions<LiveKitOptions> _options;
    private readonly RoomServiceClient _roomServiceClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoomManagementService"/> class with the provided HTTP client and options.
    /// </summary>
    /// <param name="httpClient">The HTTP client used for LiveKit server API calls.</param>
    /// <param name="options">The configured LiveKit options containing URL, API key, and API secret.</param>
    public RoomManagementService(HttpClient httpClient, IOptions<LiveKitOptions> options)
    {
        _options = options;
        _roomServiceClient = new RoomServiceClient(_options.Value.Url, _options.Value.ApiKey, _options.Value.ApiSecret, httpClient);
    }

    // Rooms

    /// <inheritdoc />
    public Task<Room> CreateRoomAsync(CreateRoomRequest request) => _roomServiceClient.CreateRoom(request);

    /// <inheritdoc />
    public Task<ListRoomsResponse> ListRoomsAsync(ListRoomsRequest request) => _roomServiceClient.ListRooms(request);

    /// <inheritdoc />
    public Task<DeleteRoomResponse> DeleteRoomAsync(DeleteRoomRequest request) => _roomServiceClient.DeleteRoom(request);

    /// <inheritdoc />
    public Task<Room> UpdateRoomMetadataAsync(UpdateRoomMetadataRequest request) => _roomServiceClient.UpdateRoomMetadata(request);

    // Participants

    /// <inheritdoc />
    public Task<ParticipantInfo> GetParticipantAsync(RoomParticipantIdentity request) => _roomServiceClient.GetParticipant(request);

    /// <inheritdoc />
    public Task<ListParticipantsResponse> ListParticipantsAsync(ListParticipantsRequest request) => _roomServiceClient.ListParticipants(request);

    /// <inheritdoc />
    public Task<MoveParticipantResponse> MoveParticipantAsync(MoveParticipantRequest request) => _roomServiceClient.MoveParticipant(request);

    /// <inheritdoc />
    public Task<RemoveParticipantResponse> RemoveParticipantAsync(RoomParticipantIdentity request) => _roomServiceClient.RemoveParticipant(request);

    /// <inheritdoc />
    public Task<ParticipantInfo> UpdateParticipantAsync(UpdateParticipantRequest request) => _roomServiceClient.UpdateParticipant(request);

    /// <inheritdoc />
    public Task<ForwardParticipantResponse> ForwardParticipantAsync(ForwardParticipantRequest request) => _roomServiceClient.ForwardParticipant(request);

    // Others

    /// <inheritdoc />
    public Task<UpdateSubscriptionsResponse> UpdateSubscriptionsAsync(UpdateSubscriptionsRequest request) => _roomServiceClient.UpdateSubscriptions(request);

    /// <inheritdoc />
    public Task<MuteRoomTrackResponse> MutePublishedTrackAsync(MuteRoomTrackRequest request) => _roomServiceClient.MutePublishedTrack(request);

    /// <inheritdoc />
    public Task<SendDataResponse> SendDataAsync(SendDataRequest request) => _roomServiceClient.SendData(request);
}
