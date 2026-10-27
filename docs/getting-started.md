# Getting started

## AppHost

Add the hosting package to the AppHost project. `AddLiveKit` generates secret
credentials and persists them in the AppHost project's user secrets during
local runs, so no parameter prompts are required:

```csharp
var redis = builder.AddRedis("redis");

var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis)
    .WithTurn();

var api = builder.AddProject<Projects.Api>("api")
    .WithLiveKitReference(livekit, includeCredentials: true);
```

When using Aspire's `RedisResource`, `.WithRedis(redis)` configures LiveKit
with the Redis host and port, and forwards any configured password and TLS
setting as separate LiveKit Redis options. Do not pass an Aspire Redis
connection string directly as a LiveKit `redis.address`; it can include
connection options that LiveKit does not interpret as part of the address.

`WithLiveKitReference` always provides the HTTP server URL. Credentials are
added to a consuming project only when `includeCredentials` is `true`; use that
option only for trusted server-side projects. Never pass the API secret to
browsers or other untrusted clients. Issue short-lived access tokens from a
trusted backend instead.

The generated API key and secret use the parameter names `livekit-api-key` and
`livekit-api-secret`. The secret is generated with at least 64 characters
(ASCII alphanumeric, therefore at least 64 bytes), exceeding the upstream .NET
server SDK's 32-byte minimum in every environment. The .NET integration also
validates this requirement at startup. The same key/secret pair is configured
for the LiveKit server and consuming application. You may instead create your
own secret parameters and pass them to the overload:

```csharp
var apiKey = builder.AddParameter("livekit-api-key", secret: true);
var apiSecret = builder.AddParameter("livekit-api-secret", secret: true);
var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret, version: "v1.9.0");
```

The initial server API endpoint is HTTP on port 7880. TCP RTC port 7881 and UDP
mux port 7882 are exposed separately. These ports are configurable through
`AddLiveKit`'s `httpPort`, `rtcTcpPort`, and `rtcUdpPort` arguments. Enabling
embedded TURN exposes the configured TURN/UDP port (3478 by default):

```csharp
var livekit = builder.AddLiveKit("livekit")
    .WithTurn(udpPort: 3478)
    .WithTurnTls(
        domain: "turn.example.test",
        certificateFile: "/certs/turn.crt",
        keyFile: "/certs/turn.key");
```

LiveKit JSON logging is enabled by default. It formats server logs as JSON on
the container console:

```csharp
var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis);
```

These records are visible in the resource's console logs. To also send parsed
records to Aspire's Structured Logs view over OTLP, opt in to forwarding:

```csharp
var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis)
    .WithStructuredLogs();
```

The AppHost must have an OTLP endpoint configured through
`ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL`, `OTEL_EXPORTER_OTLP_ENDPOINT`, or
`OTEL_EXPORTER_OTLP_LOGS_ENDPOINT`. The same `.WithStructuredLogs()` opt-in is
available on LiveKit Ingress, Egress, and SIP resources. Server JSON records are
forwarded with their parsed fields; companion-service JSON records are parsed
when available, and plain-text records are forwarded as message-only logs.
Call `.WithJsonLogging()` on Ingress, Egress, or SIP to enable JSON console
output without enabling OTLP forwarding. `.WithStructuredLogs()` enables JSON
logging automatically for the selected resource.

```csharp
var ingress = livekit.AddIngress("ingress", redis, rtmpBaseUrl, whipBaseUrl)
    .WithJsonLogging();
var egress = livekit.AddEgress("egress", redis)
    .WithJsonLogging();
var sip = livekit.AddSip("sip", redis)
    .WithJsonLogging();
```

Mount the certificate and key into the LiveKit container at those paths using
Aspire's bind-mount API. These local endpoint bindings do not configure public
DNS, TLS termination, firewalls, or production load balancing.

Ingress is a separate resource and requires the same Redis resource as the
server. Supply externally reachable publishing URLs explicitly:

```csharp
var ingress = livekit.AddIngress(
    "ingress",
    redis,
    rtmpBaseUrl: "rtmp://localhost:1935/live",
    whipBaseUrl: "http://localhost:8080/whip");
```

The RTMP/WHIP URLs are advertised to publishers and must be reachable from
those publishers. The RTMP listener uses TCP 1935; WHIP uses TCP 8080 and UDP
7885 by default. These listener ports can be overridden with the optional
`rtmpPort`, `whipPort`, and `whipUdpPort` arguments. Aspire's local container
network by itself does not make these addresses publicly reachable.

## Optional Egress service

Egress is independent from Ingress and starts only when added. It requires the
same LiveKit server and Redis resource:

```csharp
var egress = livekit.AddEgress(
        "egress",
        redis,
        healthPort: 8081,
        prometheusPort: 9090)
    .WithEgressLocalOutputDirectory("./recordings", "/out");
```

The local directory is mounted read/write at `/out`; file-output requests sent
to LiveKit must use a path under that container directory. A bind mount does
not make the directory available to an Egress job unless its output request
names the mounted path. Cloud upload settings are optional and can instead be
configured with secret Aspire parameters:

```csharp
var accessKey = builder.AddParameter("egress-access-key", secret: true);
var accessSecret = builder.AddParameter("egress-access-secret", secret: true);

var egress = livekit.AddEgress("egress", redis)
    .WithEgressS3Storage("recordings", "us-east-1", accessKey, accessSecret);
```

`WithEgressAzureStorage` is also available for Azure Blob Storage. Neither
method creates a bucket/container or provisions credentials. Egress can also
use per-request storage settings; this integration does not claim that a
recording or stream destination is ready until the Egress API request supplies
a valid output. Local output is not durable unless the mounted directory is
backed by appropriate storage. The LiveKit Egress documentation recommends at
least 4 CPUs and 4 GB of memory for an instance; allocate resources using the
container runtime or deployment platform.

Health and Prometheus ports are opt-in. Egress `latest` is mutable; pass an
independent `version` to pin its image. The advanced
`WithEgressAdditionalConfiguration` method appends Egress YAML settings, but
rejects settings managed by the typed server, Redis, port, and storage options.

## Optional SIP service

SIP is independent from both Ingress and Egress. It uses the same server and
Redis connection:

```csharp
var sip = livekit.AddSip(
    "sip",
    redis,
    sipPort: 5060,
    rtpPortStart: 10000,
    rtpPortEnd: 20000,
    healthPort: 8082);
```

The default SIP signaling port is exposed over both UDP and TCP. SIP TLS is
optional; provide certificate and key paths that exist inside the container,
and mount the files separately using Aspire's container bind-mount API:

```csharp
var sip = livekit.AddSip("sip", redis)
    .WithSipTls("/certs/sip.crt", "/certs/sip.key");
```

The default RTP media range is UDP 10000–20000. Aspire endpoint bindings
represent individual ports, not this full range; for the default range, arrange
the UDP port-range publishing and public firewall routing in the deployment
platform. A single RTP port can be configured by setting `rtpPortStart` and
`rtpPortEnd` to the same value, which adds an Aspire UDP endpoint. LiveKit's
SIP documentation requires public reachability for the configured SIP and RTP
ports; local container networking alone does not provide telephony access.

SIP provider accounts, trunks, and dispatch rules are not created by this
resource. Configure them separately through the LiveKit APIs and your
telephony provider. The optional `WithSipAdditionalConfiguration` method
appends SIP YAML settings while preventing overrides of the typed server,
Redis, listener, health, metrics, and TLS configuration.

Image tags default to mutable `latest`; specify explicit tags for repeatable
builds and deployments. Pin AppHost integrations, the wrapper library, and
container image versions independently.

## .NET application (ASP.NET Core or console)

Reference `Fstonge.Aspire.Sdk.LiveKit` and register the SDK from standard .NET
configuration:

```csharp
builder.Services.AddLiveKitSdk(builder.Configuration);
```

The wrapper accepts either these root configuration keys (including
environment variables) or the corresponding `LiveKit` section values:

| Key | Required |
|---|---|
| `LIVEKIT_URL` | Yes; absolute HTTP or HTTPS URL |
| `LIVEKIT_API_KEY` | Yes |
| `LIVEKIT_API_SECRET` | Yes; at least 32 UTF-8 bytes for the upstream SDK |

The SDK package targets .NET 10 and uses the generic `Microsoft.Extensions`
configuration and dependency-injection abstractions. It works in console apps,
worker services, and ASP.NET Core applications; it does not depend on the
ASP.NET Core shared framework.

`LIVEKIT_URL` is an HTTP(S) API URL for the upstream server SDK; WebSocket
`ws://` and `wss://` client URLs are not accepted. The package registers
`RoomServiceClient` as a transient service because the upstream client mutates
per-request authorization headers. It validates required settings when the
service is resolved or the host starts. Validation errors identify missing
settings but never include credential values.

The LiveKit API key/secret pair is configured on the self-hosted server; LiveKit
does not issue these credentials automatically. They are used by trusted
server-side applications and to create access tokens. Client applications
should receive only short-lived tokens, never the API secret.

## RTC participant connections

Applications that need to join LiveKit rooms as participants can use the
separate `Fstonge.Aspire.Rtc.LiveKit` package. It reads the same
`LIVEKIT_URL`/`LiveKit:Url` setting and translates HTTP(S) to WS(S):

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

Provide a short-lived participant token from a trusted backend; this wrapper
does not use the server API secret. Every connected `Room` is a live session
owned by its caller and must be disposed. The upstream RTC package includes
native FFI binaries, so verify your deployment's operating system and
architecture are supported.
