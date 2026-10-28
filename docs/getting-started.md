# Getting started

This guide covers a minimal Aspire setup and connecting .NET applications to
LiveKit. For more detail, see the [optional services guide](optional-services.md),
the [agents guide](agents.md), and the
[image configuration reference](image-configuration.md).

## AppHost

Add the hosting package to the AppHost project and configure LiveKit with a
shared Redis resource:

```csharp
var redis = builder.AddRedis("redis");

var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis);

var api = builder.AddProject<Projects.Api>("api")
    .WithLiveKitReference(livekit, includeCredentials: true)
    .WaitFor(livekit);
```

`AddLiveKit("livekit")` generates the API key and secret as Aspire secret
parameters. During local runs, Aspire persists them in the AppHost project's
user secrets. To supply your own credentials, create secret parameters:

```csharp
var apiKey = builder.AddParameter("livekit-api-key", secret: true);
var apiSecret = builder.AddParameter("livekit-api-secret", secret: true);

var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret, version: "v1.9.0")
    .WithRedis(redis);
```

`WithLiveKitReference` always provides the server URL. It adds credentials to
the consuming project only when `includeCredentials` is `true`; use this only
for trusted server-side applications. Never send the API secret to browsers or
other untrusted clients. Issue short-lived participant tokens from a trusted
backend instead.

The server API listens on HTTP port 7880, TCP RTC port 7881, and UDP RTC mux
port 7882 by default. These can be overridden with the `httpPort`,
`rtcTcpPort`, and `rtcUdpPort` arguments to `AddLiveKit`. For embedded TURN:

```csharp
var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis)
    .WithTurn(udpPort: 3478);
```

Aspire captures container console output. Call `.WithStructuredLogs()` on a
resource to also forward logs to the dashboard's Structured Logs view over
OTLP. This requires an OTLP endpoint on the AppHost. See the [image
configuration reference](image-configuration.md) for logging and additional
configuration options.

Ingress, Egress, and SIP are optional resources. See the
[optional services guide](optional-services.md) for their setup, storage, and
networking requirements.

## .NET server API

Reference `Fstonge.Aspire.Sdk.LiveKit` and register the server SDK:

```csharp
builder.Services.AddLiveKitSdk(builder.Configuration);
```

The wrapper reads `LIVEKIT_URL`, `LIVEKIT_API_KEY`, and
`LIVEKIT_API_SECRET` from environment variables or the `LiveKit` configuration
section. The URL must be an absolute HTTP or HTTPS API URL; the secret must be
at least 32 bytes. `IAccessTokenFactory` is registered as a singleton for
constructing `AccessToken` values from the configured key and secret:

```csharp
var tokenFactory = serviceProvider.GetRequiredService<IAccessTokenFactory>();
var accessToken = tokenFactory.GetAccessToken();
```

`IRoomManagementService` is also registered as an HTTP-backed wrapper over the
LiveKit room APIs, so trusted server code can manage rooms and participants
without directly working with the lower-level client:

```csharp
var roomService = serviceProvider.GetRequiredService<IRoomManagementService>();
var room = await roomService.CreateRoomAsync(new CreateRoomRequest { Name = "demo-room" });

await roomService.DeleteRoomAsync(new DeleteRoomRequest { Room = room.Name });
```

Settings are validated when the host starts or the client is resolved.
Validation errors do not expose credential values.

## RTC participant connections

To connect a .NET application to a room as a participant, reference
`Fstonge.Aspire.Rtc.LiveKit` and register its room factory:

```csharp
builder.Services.AddLiveKitRtcSdk(builder.Configuration);

var roomFactory = serviceProvider.GetRequiredService<ILiveKitRtcRoomFactory>();
var room = await roomFactory.ConnectAsync(participantToken);
try
{
    // Interact with the connected participant and tracks.
}
finally
{
    room.Dispose();
}
```

Provide a short-lived participant token from a trusted backend; this package
does not use the server API secret. Each connected room belongs to the caller
and must be disposed. The upstream RTC package includes native FFI binaries,
so check that your deployment's operating system and architecture are
supported.
