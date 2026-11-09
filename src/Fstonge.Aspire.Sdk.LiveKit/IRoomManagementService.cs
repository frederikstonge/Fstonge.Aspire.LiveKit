using Livekit.Server.Sdk.Dotnet;

namespace Fstonge.Aspire.Sdk.LiveKit;

/// <summary>
/// Service for managing LiveKit rooms, participants, track subscriptions, and data messages.
/// </summary>
public interface IRoomManagementService
{
    /// <summary>
    /// Creates a new LiveKit room with the specified configuration.
    /// </summary>
    /// <param name="request">The room creation parameters.</param>
    /// <returns>A task that represents the asynchronous operation, containing the created <see cref="Room"/>.</returns>
    Task<Room> CreateRoomAsync(CreateRoomRequest request);

    /// <summary>
    /// Deletes an existing LiveKit room and disconnects all participants.
    /// </summary>
    /// <param name="request">The room deletion parameters specifying the room name.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="DeleteRoomResponse"/>.</returns>
    Task<DeleteRoomResponse> DeleteRoomAsync(DeleteRoomRequest request);

    /// <summary>
    /// Forwards participant tracks to another room.
    /// </summary>
    /// <param name="request">The parameters specifying participant and destination room.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="ForwardParticipantResponse"/>.</returns>
    Task<ForwardParticipantResponse> ForwardParticipantAsync(ForwardParticipantRequest request);

    /// <summary>
    /// Retrieves details and current status of a participant in a room.
    /// </summary>
    /// <param name="request">The participant and room identity parameters.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="ParticipantInfo"/>.</returns>
    Task<ParticipantInfo> GetParticipantAsync(RoomParticipantIdentity request);

    /// <summary>
    /// Lists all active participants in a room.
    /// </summary>
    /// <param name="request">The request parameters specifying the room name.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="ListParticipantsResponse"/>.</returns>
    Task<ListParticipantsResponse> ListParticipantsAsync(ListParticipantsRequest request);

    /// <summary>
    /// Lists active LiveKit rooms matching the request filters.
    /// </summary>
    /// <param name="request">The query parameters to filter rooms.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="ListRoomsResponse"/>.</returns>
    Task<ListRoomsResponse> ListRoomsAsync(ListRoomsRequest request);

    /// <summary>
    /// Moves a participant from one room to another.
    /// </summary>
    /// <param name="request">The parameters specifying the participant and target room.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="MoveParticipantResponse"/>.</returns>
    Task<MoveParticipantResponse> MoveParticipantAsync(MoveParticipantRequest request);

    /// <summary>
    /// Mutes or unmutes a track published by a participant in a room.
    /// </summary>
    /// <param name="request">The parameters identifying the room, participant, track, and muted state.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="MuteRoomTrackResponse"/>.</returns>
    Task<MuteRoomTrackResponse> MutePublishedTrackAsync(MuteRoomTrackRequest request);

    /// <summary>
    /// Disconnects and removes a participant from a room.
    /// </summary>
    /// <param name="request">The participant and room identity parameters.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="RemoveParticipantResponse"/>.</returns>
    Task<RemoveParticipantResponse> RemoveParticipantAsync(RoomParticipantIdentity request);

    /// <summary>
    /// Sends a data packet (reliable or lossy) to participants in a room.
    /// </summary>
    /// <param name="request">The data payload and targeting parameters.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="SendDataResponse"/>.</returns>
    Task<SendDataResponse> SendDataAsync(SendDataRequest request);

    /// <summary>
    /// Updates metadata, name, or permissions for an existing participant.
    /// </summary>
    /// <param name="request">The update parameters identifying the participant and changes.</param>
    /// <returns>A task that represents the asynchronous operation, containing the updated <see cref="ParticipantInfo"/>.</returns>
    Task<ParticipantInfo> UpdateParticipantAsync(UpdateParticipantRequest request);

    /// <summary>
    /// Updates metadata for an existing room.
    /// </summary>
    /// <param name="request">The room metadata update parameters.</param>
    /// <returns>A task that represents the asynchronous operation, containing the updated <see cref="Room"/>.</returns>
    Task<Room> UpdateRoomMetadataAsync(UpdateRoomMetadataRequest request);

    /// <summary>
    /// Updates track subscription settings for a participant in a room.
    /// </summary>
    /// <param name="request">The subscription update parameters specifying tracks to subscribe or unsubscribe.</param>
    /// <returns>A task that represents the asynchronous operation, containing the <see cref="UpdateSubscriptionsResponse"/>.</returns>
    Task<UpdateSubscriptionsResponse> UpdateSubscriptionsAsync(UpdateSubscriptionsRequest request);
}
