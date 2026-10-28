# Image configuration reference

This page lists every setting exposed directly by the Aspire integration for
its four container images. Settings accepted by newer upstream image versions
may not have a typed property here; use the corresponding additional
configuration method when the key is not managed by the integration. Consult
the upstream references for the complete, version-specific YAML
configuration.

For a minimal Aspire setup, see the [getting-started guide](getting-started.md).
For optional Ingress, Egress, and SIP setup, see the
[optional services guide](optional-services.md).

## Typed and additional configuration

Typed configuration callbacks expose common settings for each service.
Additional YAML methods support top-level settings available in the selected
upstream image version:

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

## LiveKit server (`livekit/livekit-server`)

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

## Ingress (`livekit/ingress`)

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

## Egress (`livekit/egress`)

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

## SIP (`livekit/sip`)

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
