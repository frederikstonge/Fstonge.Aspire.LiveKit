# Fstonge.Aspire.Sdk.LiveKit

Configures the upstream `Livekit.Server.Sdk.Dotnet` SDK for a trusted .NET
server application:

```csharp
builder.Services.AddLiveKitSdk(builder.Configuration);
```

Set `LIVEKIT_URL`, `LIVEKIT_API_KEY`, and `LIVEKIT_API_SECRET` using
environment variables, user secrets, or the `LiveKit` configuration section.
The wrapper registers `IRoomManagementService` as an HTTP-backed service for
LiveKit room and participant management, and `IAccessTokenFactory` as a
singleton for creating `AccessToken` values from the configured API key and
secret. For example:

```csharp
var tokenFactory = serviceProvider.GetRequiredService<IAccessTokenFactory>();
var accessToken = tokenFactory.GetAccessToken();
```

It also registers `IRoomManagementService` as an HTTP-backed service for LiveKit
room and participant management operations such as creating rooms, listing
participants, updating metadata, and deleting rooms:

```csharp
var roomService = serviceProvider.GetRequiredService<IRoomManagementService>();
var room = await roomService.CreateRoomAsync(new CreateRoomRequest { Name = "demo-room" });

await roomService.DeleteRoomAsync(new DeleteRoomRequest { Room = room.Name });
```

It validates settings without writing credential values to errors. See the
repository's [getting-started guide](../../docs/getting-started.md) for Aspire
setup and security guidance.

The package targets .NET 10.

This wrapper is built on
[Pablo Fuente's `Livekit.Server.Sdk.Dotnet`](https://github.com/pabloFuente/livekit-server-sdk-dotnet/tree/main/LivekitApi).
Many thanks to Pablo for bringing LiveKit's server APIs to .NET.

For applications that join LiveKit rooms as participants, use the separate
[`Fstonge.Aspire.Rtc.LiveKit`](../Fstonge.Aspire.Rtc.LiveKit/README.md)
package. This package manages server APIs and access tokens; the RTC package
creates participant connections using short-lived tokens.
