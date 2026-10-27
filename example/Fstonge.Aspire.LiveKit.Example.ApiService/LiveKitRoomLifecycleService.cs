using Fstonge.Aspire.Rtc.LiveKit;
using Fstonge.Aspire.Sdk.LiveKit;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Options;
using RtcRoom = LiveKit.Rtc.Room;

namespace Fstonge.Aspire.LiveKit.Example.ApiService;

public sealed class LiveKitRoomLifecycleService(
    IServiceScopeFactory scopeFactory,
    ILiveKitRtcRoomFactory rtcRoomFactory,
    IOptions<LiveKitOptions> options,
    ILogger<LiveKitRoomLifecycleService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var roomName = $"aspire-smoke-{Guid.NewGuid():N}";
        var participantIdentity = $"aspire-smoke-{Guid.NewGuid():N}";
        var liveKitOptions = options.Value;

        await using var scope = scopeFactory.CreateAsyncScope();
        var roomService = scope.ServiceProvider.GetRequiredService<RoomServiceClient>();
        var roomCreated = false;

        try
        {
            stoppingToken.ThrowIfCancellationRequested();
            await roomService.CreateRoom(new CreateRoomRequest { Name = roomName });
            roomCreated = true;
            logger.LogInformation("Created LiveKit room {RoomName}", roomName);

            var participantToken = new AccessToken(liveKitOptions.ApiKey!, liveKitOptions.ApiSecret!)
                .WithIdentity(participantIdentity)
                .WithName(participantIdentity)
                .WithGrants(new VideoGrants { RoomJoin = true, Room = roomName })
                .WithTtl(TimeSpan.FromMinutes(1))
                .ToJwt();

            stoppingToken.ThrowIfCancellationRequested();
            RtcRoom? room = null;
            try
            {
                room = await rtcRoomFactory.ConnectAsync(participantToken);
                logger.LogInformation("Connected RTC participant to LiveKit room {RoomName}", roomName);
            }
            finally
            {
                if (room is not null)
                {
                    try
                    {
                        await room.DisconnectAsync();
                        logger.LogInformation("Disconnected RTC participant from LiveKit room {RoomName}", roomName);
                    }
                    finally
                    {
                        room.Dispose();
                    }
                }
            }
        }
        finally
        {
            if (roomCreated)
            {
                await roomService.DeleteRoom(new DeleteRoomRequest { Room = roomName });
                logger.LogInformation("Deleted LiveKit room {RoomName}", roomName);
            }
        }
    }
}
