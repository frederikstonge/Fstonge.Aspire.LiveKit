# Getting started

## AppHost

Add the hosting package to the AppHost project. No API key or secret arguments
are required: `AddLiveKit` generates them as secret parameters and persists
them in the AppHost project's user secrets during local runs, so no parameter
prompts are required:

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
for the LiveKit server and, when `includeCredentials: true`, the consuming
application. To use your own credentials instead, create secret parameters
and pass them to the overload:

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

The hosting integration provides typed methods for common settings and YAML
extension methods for options added by upstream LiveKit versions:

```csharp
var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis)
    .WithLiveKitServerConfiguration(configuration =>
    {
        configuration.LogLevel = "info";
        configuration.Rtc.UseExternalIp = true;
        configuration.Rtc.PortRangeStart = 50000;
        configuration.Rtc.PortRangeEnd = 60000;
    })
    .WithLiveKitAdditionalConfiguration("""
        room:
          auto_create: false
        """);

var ingress = livekit.AddIngress(
        "ingress",
        redis,
        "rtmp://localhost:1935/live",
        "http://localhost:8080/whip")
    .WithIngressConfiguration(configuration =>
    {
        configuration.CpuCost.RtmpCpuCost = 2;
        configuration.CpuCost.WhipCpuCost = 2;
        configuration.CpuCost.WhipBypassTranscodingCpuCost = 0.1;
        configuration.CpuCost.UrlCpuCost = 2;
    });

var egress = livekit.AddEgress("egress", redis)
    .WithEgressConfiguration(configuration =>
    {
        configuration.TemplatePort = 7980;
        configuration.Logging.Level = "info";
    });

var sip = livekit.AddSip("sip", redis)
    .WithSipConfiguration(configuration =>
    {
        configuration.MaxCpuUtilization = 0.9;
        configuration.EnableJitterBuffer = true;
        configuration.Logging.Level = "info";
    });
```

Typed configuration objects are available for common settings on all four
services. Their nested options (`Rtc`, `CpuCost`, and `Logging`) are initialized
and can be configured directly in the callback; unset nested options are omitted
from the generated YAML. `WithLiveKitAdditionalConfiguration`,
`WithIngressAdditionalConfiguration`, `WithEgressAdditionalConfiguration`,
and `WithSipAdditionalConfiguration` accept additional top-level settings
supported by each service version. Settings already managed by typed resource
options (such as credentials, Redis, listener ports, TLS, storage, and JSON
logging) are reserved and cannot be duplicated. Typed settings are serialized
with YamlDotNet, and additional YAML is parsed to validate its top-level shape
and check reserved keys. `Logging.Level` accepts `debug`, `info`, `warn`, or
`error`; use `.WithJsonLogging()` or `.WithStructuredLogs()` to enable JSON
logging.

## Image configuration reference

The following lists every setting exposed directly by this Aspire integration
for its four container images. Settings accepted by newer upstream image
versions may not have a typed property here; use the corresponding additional
configuration method when the key is not managed by the integration. Consult
the upstream references for the complete, version-specific YAML configuration.

### LiveKit server (`livekit/livekit-server`)

| Configuration | Aspire API | Default or notes |
|---|---|---|
| Image version | `AddLiveKit(version: ...)` | `latest` |
| HTTP API host port | `AddLiveKit(httpPort: ...)` | `7880`, mapped to container port `7880` |
| RTC TCP port | `AddLiveKit(rtcTcpPort: ...)` | `7881` |
| RTC UDP mux port | `AddLiveKit(rtcUdpPort: ...)` | `7882` |
| API key and secret | `AddLiveKit(name)` or `AddLiveKit(name, apiKey, apiSecret)` | Generated as persisted secret parameters, or supplied as secret parameters |
| Redis | `WithRedis(redis)` | Required for distributed operation and shared with companion services |
| Log level | `WithLiveKitServerConfiguration(c => c.LogLevel = ...)` | Optional override; `debug`, `info`, `warn`, or `error` |
| RTC external IP discovery | `c.Rtc.UseExternalIp` | Optional; unset by default |
| RTC media UDP range | `c.Rtc.PortRangeStart` and `c.Rtc.PortRangeEnd` | Optional; set both together; both must be in `1`–`65535` |
| Embedded TURN/UDP | `WithTurn(udpPort: ...)` | Optional; port defaults to `3478` |
| Embedded TURN/TLS | `WithTurnTls(domain, certificateFile, keyFile, tlsPort: ...)` | Optional; TLS port defaults to `5349`; mount certificate and key into the container |
| Additional YAML | `WithLiveKitAdditionalConfiguration(yaml)` | Adds upstream settings not already managed by the integration |
| JSON logs / structured logs | `WithJsonLogging()` / `WithStructuredLogs()` | Server JSON console logging is enabled by default; structured forwarding requires an AppHost OTLP endpoint |

The integration manages the server's `development`, `port`, `log_level`,
`rtc`, `redis`, `turn`, `ingress`, `logging`, and `keys` top-level YAML
settings. Configure those through the corresponding Aspire APIs rather than
trying to override them with additional YAML. The [LiveKit server deployment
guide](https://docs.livekit.io/transport/self-hosting/deployment.md) and
[upstream `config-sample.yaml`](https://github.com/livekit/livekit/blob/master/config-sample.yaml)
document the full server configuration.

### Ingress (`livekit/ingress`)

| Configuration | Aspire API | Default or notes |
|---|---|---|
| Image version | `AddIngress(version: ...)` | `latest` |
| RTMP and WHIP advertised base URLs | `AddIngress(..., rtmpBaseUrl, whipBaseUrl)` | Required; RTMP(S) and HTTP(S) respectively |
| RTMP listener port | `AddIngress(rtmpPort: ...)` | `1935` |
| WHIP TCP listener port | `AddIngress(whipPort: ...)` | `8080` |
| WHIP/RTC UDP port | `AddIngress(whipUdpPort: ...)` | `7885` |
| API credentials and LiveKit WebSocket URL | Supplied from the parent LiveKit resource | Written to the container environment/configuration |
| Redis | `AddIngress(..., redis, ...)` | Required; use the same Redis resource as the server |
| CPU admission costs | `WithIngressConfiguration(c => c.CpuCost...)` | `RtmpCpuCost`, `WhipCpuCost`, `WhipBypassTranscodingCpuCost`, and `UrlCpuCost`; unset by default |
| Minimum idle CPU ratio | `c.CpuCost.MinIdleRatio` | Optional; must be greater than `0` and at most `1` |
| Log level | `c.Logging.Level` | Optional override; `debug`, `info`, `warn`, or `error` |
| JSON logs / structured logs | `WithJsonLogging()` / `WithStructuredLogs()` | JSON output is opt-in |
| Additional YAML | `WithIngressAdditionalConfiguration(yaml)` | Adds upstream settings not managed by the integration |

The base URLs are configured on the LiveKit server by `AddIngress`; they are
used to advertise publishing addresses and must be reachable by publishers.
The [Ingress self-hosting configuration
guide](https://docs.livekit.io/transport/self-hosting/ingress.md) and
[upstream Ingress configuration](https://github.com/livekit/ingress#config)
document the full Ingress configuration, including options that can be supplied
through additional YAML.

### Egress (`livekit/egress`)

| Configuration | Aspire API | Default or notes |
|---|---|---|
| Image version | `AddEgress(version: ...)` | `latest` |
| Health-check port | `AddEgress(healthPort: ...)` | Optional |
| Prometheus metrics port | `AddEgress(prometheusPort: ...)` | Optional |
| API credentials and LiveKit WebSocket URL | Supplied from the parent LiveKit resource | Written to the container environment |
| Redis | `AddEgress(..., redis)` | Required; use the same Redis resource as the server |
| Local output directory | `WithEgressLocalOutputDirectory(hostPath, containerPath)` | Optional; container path defaults to `/out` |
| S3 storage | `WithEgressS3Storage(bucket, region, accessKey, secret, endpoint)` | Optional; credentials are Aspire parameters; endpoint is optional |
| Azure Blob Storage | `WithEgressAzureStorage(containerName, accountName, accountKey)` | Optional; credentials are Aspire parameters |
| Template server port | `WithEgressConfiguration(c => c.TemplatePort = ...)` | Optional; upstream default is `7980`; valid ports are `1`–`65535` |
| Debug handler port | `c.DebugHandlerPort` | Optional; `0` disables it upstream; when specified here, valid ports are `1`–`65535` |
| PulseAudio sink reap grace period | `c.PulseSinkReapGraceSeconds` | Optional; upstream default is `30` seconds; negative disables reaping |
| Custom template base URL | `c.TemplateBase` | Optional; upstream default is `http://localhost:<template_port>/` |
| Chrome sandbox | `c.EnableChromeSandbox` | Optional; upstream default is `false` |
| Log level | `c.Logging.Level` | Optional override; `debug`, `info`, `warn`, or `error` |
| JSON logs / structured logs | `WithJsonLogging()` / `WithStructuredLogs()` | JSON output is opt-in |
| Additional YAML | `WithEgressAdditionalConfiguration(yaml)` | Adds upstream settings not managed by the integration |

Only one storage provider can be configured per Egress resource. The integration
currently provides typed S3 and Azure storage helpers; the upstream Egress
configuration also documents GCP storage, which this integration does not
currently expose: the `storage` key is reserved and cannot be supplied through
additional YAML. The [Egress self-hosting configuration
guide](https://docs.livekit.io/transport/self-hosting/egress.md) and
[upstream Egress configuration](https://github.com/livekit/egress#config)
document all options supported by the selected image version.

### SIP (`livekit/sip`)

| Configuration | Aspire API | Default or notes |
|---|---|---|
| Image version | `AddSip(version: ...)` | `latest` |
| SIP signaling port | `AddSip(sipPort: ...)` | `5060`, exposed over UDP and TCP |
| RTP UDP port range | `AddSip(rtpPortStart: ..., rtpPortEnd: ...)` | `10000`–`20000`; the range is configured in the container |
| Health-check port | `AddSip(healthPort: ...)` | Optional |
| Prometheus metrics port | `AddSip(prometheusPort: ...)` | Optional |
| SIP TLS | `WithSipTls(certificateFile, keyFile, tlsPort: ...)` | Optional; port defaults to `5061`; mount certificate and key into the container |
| API credentials and LiveKit WebSocket URL | Supplied from the parent LiveKit resource | Written to the container environment |
| Redis | `AddSip(..., redis)` | Required; use the same Redis resource as the server |
| Cluster ID | `WithSipConfiguration(c => c.ClusterId = ...)` | Optional |
| Maximum CPU utilization | `c.MaxCpuUtilization` | Optional; greater than `0` and at most `1` |
| Maximum active calls | `c.MaxActiveCalls` | Optional; non-negative |
| Signaling external IP / local network / NAT address | `c.UseExternalIp`, `c.LocalNet`, `c.Nat1To1Ip` | Optional; external-IP discovery and static NAT address cannot both be enabled |
| Media external IP / NAT address | `c.MediaUseExternalIp`, `c.MediaNat1To1Ip` | Optional; external-IP discovery and static NAT address cannot both be enabled |
| Maximum UDP payload | `c.UdpMaxPayload` | Optional; when specified, must be positive |
| Jitter buffer | `c.EnableJitterBuffer`, `c.EnableJitterBufferProbability` | Optional; probability must be between `0` and `1` |
| Log level | `c.Logging.Level` | Optional override; `debug`, `info`, `warn`, or `error` |
| JSON logs / structured logs | `WithJsonLogging()` / `WithStructuredLogs()` | JSON output is opt-in |
| Additional YAML | `WithSipAdditionalConfiguration(yaml)` | Adds upstream settings not managed by the integration |

The [LiveKit SIP service configuration](https://github.com/livekit/sip#config)
documents SIP YAML options supported by the selected image version. SIP trunks
and dispatch rules are separate LiveKit API resources; adding the SIP container
does not create them.

Mount the certificate and key into the LiveKit container at those paths using
Aspire's bind-mount API. These local endpoint bindings do not configure public
DNS, TLS termination, firewalls, or production load balancing.

Ingress is a separate resource and requires the same Redis resource as the
server. In the Aspire dashboard, Ingress, Egress, and SIP resources appear
nested under the LiveKit server that created them. Supply externally reachable
publishing URLs explicitly:

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

The hosting integration generates the LiveKit API key/secret pair by default
and configures the self-hosted server with them; you can also provide your own
secret parameters. The credentials are used by trusted server-side
applications and to create access tokens. Client applications should receive
only short-lived tokens, never the API secret.

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
