# Fstonge.Aspire.Rtc.LiveKit

Adds dependency-injection configuration and a room connection factory for the
upstream `Livekit.Rtc.Dotnet` SDK. Use this package when a .NET service needs to
connect to a LiveKit room as a participant. It complements, but is separate
from, `Fstonge.Aspire.Sdk.LiveKit`, which configures server-management APIs.
The wrapper package targets .NET 10.

## Getting started

Register the RTC integration in a .NET application:

```csharp
using Fstonge.Aspire.Rtc.LiveKit;

builder.Services.AddLiveKitRtcSdk(builder.Configuration);
```

The package reads `LIVEKIT_URL` or `LiveKit:Url`, matching the configuration
contract used by the Aspire hosting integration. Absolute HTTP and HTTPS URLs
are converted to WS and WSS respectively; WS and WSS URLs are also accepted.
Credentials, query strings, and fragments in the server URL are rejected.

Request a participant token from a trusted backend, then connect a room:

```csharp
var rooms = serviceProvider.GetRequiredService<ILiveKitRtcRoomFactory>();
var room = await rooms.ConnectAsync(participantToken);

try
{
    Console.WriteLine($"Connected to {room.Name}");
    // Use the room's participant, tracks, data, and RPC APIs.
}
finally
{
    room.Dispose();
}
```

The caller owns and must dispose each connected room. The factory is safe to
register as a singleton because it stores only the normalized server URL and
creates a new `Room` for each connection. Do not register a connected `Room`
as a singleton or share it between unrelated participants.

The RTC SDK uses a participant access token, not the LiveKit server API secret.
Keep token creation in a trusted service using `Fstonge.Aspire.Sdk.LiveKit` (or
another server SDK); do not expose the server API secret to clients.

The upstream RTC package includes native FFI assets for supported Windows,
Linux, and macOS x64 and ARM64 targets. Verify that the deployment runtime is
supported and that the package's native runtime asset is included when
publishing or running in containers.
