# Getting started

## AppHost

Add the hosting package to the AppHost project and declare the credentials as
secret Aspire parameters. `AddLiveKit` creates these parameters for you when
you do not provide parameter resources explicitly:

```csharp
var redis = builder.AddRedis("redis");

var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis)
    .WithTurn();

var api = builder.AddProject<Projects.Api>("api")
    .WithLiveKitReference(livekit, includeCredentials: true);
```

`WithLiveKitReference` always provides the HTTP server URL. Credentials are
added to a consuming project only when `includeCredentials` is `true`; use that
option only for trusted server-side projects. Never pass the API secret to
browsers or other untrusted clients. Issue short-lived access tokens from a
trusted backend instead.

The generated parameter values are supplied through Aspire's normal parameter
flow (for local development, use user secrets or the AppHost secrets
configuration; the generated parameter names are `livekit-api-key` and
`livekit-api-secret`). You may also create the secret parameters yourself and
pass them to the overload:

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
| `LIVEKIT_API_SECRET` | Yes; at least 32 bytes for the upstream SDK |

The SDK package targets .NET 8 and uses the generic `Microsoft.Extensions`
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
