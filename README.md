# Fstonge.Aspire.LiveKit

Self-host LiveKit with .NET Aspire, and configure the LiveKit .NET server SDK
from the same settings in your application.

The repository contains three packages:

- `Fstonge.Aspire.Hosting.LiveKit` adds the LiveKit server and optional Ingress,
  Egress, and SIP containers to an Aspire AppHost.
- `Fstonge.Aspire.Sdk.LiveKit` configures the upstream
  `Livekit.Server.Sdk.Dotnet` client in a .NET application.
- `Fstonge.Aspire.Rtc.LiveKit` configures the upstream
  `Livekit.Rtc.Dotnet` client and provides a factory for participant room
  connections.

## Getting started

### AppHost

Add the hosting integration and Aspire Redis integration to your AppHost
project. Add a LiveKit server with a shared Redis resource:

```csharp
var redis = builder.AddRedis("redis");

var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis);

var api = builder.AddProject<Projects.Api>("api")
    .WithLiveKitReference(livekit, includeCredentials: true)
    .WaitFor(livekit);
```

`AddLiveKit` generates secret API key and API secret values if you do not
supply parameter resources. During local runs, Aspire persists the generated
values in the AppHost project's user secrets, so they remain stable across
restarts without prompting. You can also supply your own secret parameters:

```csharp
var apiKey = builder.AddParameter("livekit-api-key", secret: true);
var apiSecret = builder.AddParameter("livekit-api-secret", secret: true);

var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret, version: "v1.9.0")
    .WithRedis(redis);
```

`WithLiveKitReference` provides the server URL. Credentials are only passed to
the application when `includeCredentials` is `true`; use this only for trusted
server-side applications. Never send the API secret to browsers or other
untrusted clients. Issue short-lived access tokens from a trusted backend.

The server API listens on HTTP port 7880, TCP RTC port 7881, and UDP RTC mux
port 7882 by default. These can be overridden with the `httpPort`,
`rtcTcpPort`, and `rtcUdpPort` arguments to `AddLiveKit`. Embedded TURN is
optional:

```csharp
var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis)
    .WithTurn(udpPort: 3478);
```

LiveKit's JSON console output is enabled by default so its logs are structured
when captured by Aspire:

```csharp
var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis);
```

Aspire captures the container's console output. This configures structured JSON
logs on that output; it does not send logs directly to an OTLP collector.

Use `WithTurnTls` to configure embedded TURN/TLS. Mount its certificate and
private key into the LiveKit container at the configured paths. Aspire's local
port bindings do not configure public DNS, firewalls, TLS termination, or
production load balancing.

### .NET API service

Add a reference to `Fstonge.Aspire.Sdk.LiveKit` and register the SDK:

```csharp
builder.Services.AddLiveKitSdk(builder.Configuration);
```

The wrapper reads the following root configuration keys, including
environment variables, or values in the `LiveKit` configuration section:

| Key | Required | Validation |
|---|---|---|
| `LIVEKIT_URL` | Yes | Absolute HTTP or HTTPS API URL |
| `LIVEKIT_API_KEY` | Yes | Must be configured |
| `LIVEKIT_API_SECRET` | Yes | Must be configured and at least 32 bytes |

`RoomServiceClient` is registered as a transient typed HTTP client. Settings
are validated when the host starts or the client is resolved. Validation
messages identify missing or invalid settings without exposing credential
values.

The example API registers a startup background service that creates a
temporary room, joins it using the RTC SDK, disconnects, and deletes the room.

The wrapper targets .NET 10 and uses the generic Microsoft.Extensions
configuration and dependency-injection abstractions. It can be used in ASP.NET
Core, worker, and console applications without depending on the ASP.NET Core
shared framework.

### RTC participant connection

To connect a .NET application to a room as a participant, reference
`Fstonge.Aspire.Rtc.LiveKit` and register its factory:

```csharp
builder.Services.AddLiveKitRtcSdk(builder.Configuration);
```

The RTC integration uses the same `LIVEKIT_URL`/`LiveKit:Url` setting and
normalizes HTTP(S) URLs to WS(S). Request a short-lived participant token from
a trusted backend, then connect with a fresh room:

```csharp
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

Each connected room belongs to the caller and must be disposed. The RTC
integration does not need or accept the server API secret. The upstream RTC
package includes native FFI binaries, so check supported operating systems and
architectures for the target deployment.

## Optional services

Ingress, Egress, and SIP are separate resources. Each requires the same Redis
resource used by LiveKit and starts only when explicitly added.

### Ingress

Provide externally reachable RTMP and WHIP URLs:

```csharp
livekit.AddIngress(
    "ingress",
    redis,
    rtmpBaseUrl: "rtmp://localhost:1935/live",
    whipBaseUrl: "http://localhost:8080/whip");
```

RTMP uses TCP port 1935 by default. WHIP uses TCP port 8080 and UDP port 7885.
The advertised base URLs must be reachable by publishers; local container
networking alone does not make them public.

### Egress

Add Egress independently and optionally configure health and metrics ports:

```csharp
var egress = livekit.AddEgress(
    "egress",
    redis,
    healthPort: 8081,
    prometheusPort: 9090);
```

For local file output, mount a host directory into the Egress container and
use a matching container path in file-output requests:

```csharp
var egress = livekit.AddEgress("egress", redis)
    .WithEgressLocalOutputDirectory("./recordings", "/out");
```

S3-compatible and Azure Blob Storage settings can be supplied through the
typed storage methods. Supply credentials as secret Aspire parameters. These
methods do not create buckets or containers, provision credentials, or
configure a recording destination on a request.

### SIP

Add SIP independently and configure its signaling and RTP ports:

```csharp
var sip = livekit.AddSip(
    "sip",
    redis,
    sipPort: 5060,
    rtpPortStart: 10000,
    rtpPortEnd: 20000,
    healthPort: 8082);
```

SIP signaling uses UDP and TCP by default; optional TLS can be enabled with
`WithSipTls`, with certificate files mounted into the container. SIP providers,
trunks, and dispatch rules must be configured separately. SIP and RTP require
appropriate public routing and firewall access; Aspire endpoint bindings do
not configure external telephony networking. A default RTP range represents
many UDP ports and cannot be exposed as individual Aspire endpoints; arrange
range publishing through your deployment platform.

## Versioning and further guidance

Container image tags default to mutable `latest`. Pass explicit versions to
`AddLiveKit`, `AddIngress`, `AddEgress`, or `AddSip` for repeatable deployments.
The server, companion images, Aspire integration, and SDK wrapper can be
versioned independently.

See the [detailed getting-started guide](docs/getting-started.md) for additional
configuration and networking details, and the [example AppHost](example/Fstonge.Aspire.LiveKit.Example.AppHost/AppHost.cs)
for a working setup.

## Acknowledgements

A huge thank-you to [Pablo Fuente (@pabloFuente)](https://github.com/pabloFuente)
for creating the excellent .NET LiveKit SDKs this project builds on:

- [`Livekit.Server.Sdk.Dotnet`](https://github.com/pabloFuente/livekit-server-sdk-dotnet/tree/main/LivekitApi),
  which powers the server API integration.
- [`Livekit.Rtc.Dotnet`](https://github.com/pabloFuente/livekit-server-sdk-dotnet/tree/main/LivekitRtc),
  which powers the RTC participant integration.

Pablo's work brings LiveKit's server-management and real-time communication
capabilities to .NET. We are deeply grateful for the engineering, care, and
open-source contribution behind these libraries, and for making this Aspire
integration possible.
