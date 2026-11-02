# Image configuration reference

This page describes the configuration models and resource settings exposed by
the Aspire integration for its four container images. The generated models
cover the upstream configuration schema for the recorded source revisions;
use the corresponding additional YAML method for settings introduced by a
newer upstream version or for configuration not represented in those schemas.

For a minimal Aspire setup, see the [getting-started guide](getting-started.md).
For optional Ingress, Egress, and SIP setup, see the
[optional services guide](optional-services.md).

## Upstream configuration schemas

The weekly **Monitor LiveKit configuration schemas** workflow clones the
upstream server, Ingress, Egress, and SIP repositories and generates JSON
Schemas from their Go configuration types and YAML struct tags, then generates
C# models from those schemas. When a schema, source revision, or model changes,
the workflow opens or updates a pull request so upstream configuration changes
can be reviewed before changing this integration.

The generated schemas and source revisions are stored in
[`schemas/livekit-upstream/`](../schemas/livekit-upstream/). The generator is
documented in [`scripts/livekit-config-schema/README.md`](../scripts/livekit-config-schema/README.md).
Generated upstream model classes are in
[`src/Fstonge.Aspire.Hosting.LiveKit/UpstreamConfiguration/`](../src/Fstonge.Aspire.Hosting.LiveKit/UpstreamConfiguration/).
Schemas describe Go types and YAML tags; they do not capture all runtime
defaults, validation rules, environment-variable behavior, or custom YAML
unmarshalling. The generated C# models are the types accepted by the four
typed configuration callbacks. Their properties use C# names while
`YamlMember` aliases preserve upstream YAML keys.

## Typed and additional configuration

Typed configuration callbacks accept the generated upstream model for each
service. Aspire-owned credentials, Redis settings, listener ports, TLS, and
storage remain managed by the resource APIs and cannot be overridden through
these models. Additional YAML remains available for upstream settings absent
from the generated model; a top-level setting cannot be specified in both the
typed model and additional YAML.

```csharp
using Aspire.Hosting.LiveKit.Configuration.Egress;
using Aspire.Hosting.LiveKit.Configuration.Ingress;
using Aspire.Hosting.LiveKit.Configuration.Server;
using Aspire.Hosting.LiveKit.Configuration.Sip;

var livekit = builder.AddLiveKitServer("livekit")
    .WithLiveKitServerRedis(redis)
    .WithLiveKitServerConfiguration(configuration =>
    {
        configuration.LogLevel = "info";
        configuration.Rtc = new RTCConfig
        {
            UseExternalIp = true,
            PortRangeStart = 50000,
            PortRangeEnd = 60000,
        };
    })
    .WithLiveKitServerAdditionalConfiguration("""
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
        configuration.CpuCost = new CPUCostConfig
        {
            RtmpCpuCost = 2,
            WhipCpuCost = 2,
            WhipBypassTranscodingCpuCost = 0.1,
            UrlCpuCost = 2,
        };
    });

var egress = livekit.AddEgress("egress", redis)
    .WithEgressConfiguration(configuration =>
    {
        configuration.TemplatePort = 7980;
        configuration.EnableChromeSandbox = true;
    });

var sip = livekit.AddSip("sip", redis)
    .WithSipConfiguration(configuration =>
    {
        configuration.MaxCpuUtilization = 0.9;
        configuration.EnableJitterBuffer = true;
    });
```

The root model types are `LiveKitServerUpstreamConfiguration`,
`LiveKitIngressUpstreamConfiguration`, `LiveKitEgressUpstreamConfiguration`,
and `LiveKitSipUpstreamConfiguration`, in the service namespaces shown above.
Unset nullable properties are omitted from YAML. Generated nested model names
come from the upstream schema and can change when that schema changes.
`WithLiveKitServerAdditionalConfiguration`,
`WithIngressAdditionalConfiguration`, `WithEgressAdditionalConfiguration`,
and `WithSipAdditionalConfiguration` accept additional top-level settings
supported by each service version. Aspire-managed settings and JSON logging
remain reserved. Typed settings are serialized with YamlDotNet, and additional
YAML is parsed to validate its top-level shape, reserved keys, and duplicate
top-level settings. The models mirror upstream data types; they do not add
custom semantic validation beyond protection of settings managed by Aspire.

## LiveKit server (`livekit/livekit-server`)

| Configuration | Aspire API | Default or notes |
|---|---|---|
| Image version | `AddLiveKitServer(version: ...)` | `latest` |
| HTTP API host port | `AddLiveKitServer(httpPort: ...)` | `7880`, mapped to container port `7880` |
| RTC TCP port | `AddLiveKitServer(rtcTcpPort: ...)` | `7881` |
| RTC UDP mux port | `AddLiveKitServer(rtcUdpPort: ...)` | `7882` |
| API key and secret | `AddLiveKitServer(name)` or `AddLiveKitServer(name, apiKey, apiSecret)` | Generated as persisted secret parameters, or supplied as secret parameters |
| Redis | `WithLiveKitServerRedis(redis)` | Required for distributed operation and shared with companion services |
| Log level | `WithLiveKitServerConfiguration(c => c.LogLevel = ...)` | Optional upstream setting |
| RTC external IP discovery | `c.Rtc.UseExternalIp` | Optional upstream setting |
| RTC media UDP range | `c.Rtc.PortRangeStart` and `c.Rtc.PortRangeEnd` | Optional; configure both together, within `1`–`65535` |
| Embedded TURN/UDP | `WithLiveKitServerTurn(udpPort: ...)` | Optional; port defaults to `3478` |
| Embedded TURN/TLS | `WithLiveKitServerTurnTls(domain, certificateFile, keyFile, tlsPort: ...)` | Optional; TLS port defaults to `5349`; mount certificate and key into the container |
| Additional YAML | `WithLiveKitServerAdditionalConfiguration(yaml)` | Adds upstream settings not already managed by the integration |
| JSON logs / structured logs | `WithJsonLogging()` / `WithStructuredLogs()` | Server JSON console logging is enabled by default; structured forwarding requires an AppHost OTLP endpoint |

The integration manages the server's `development`, `port`,
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
| CPU admission costs | `WithIngressConfiguration(c => c.CpuCost = new CPUCostConfig { ... })` | Upstream settings; unset by default |
| Minimum idle CPU ratio | `c.CpuCost.MinIdleRatio` | Optional upstream setting |
| Logging | `c.Logging` | Upstream logging options; JSON formatting is managed by Aspire |
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
| Template server port | `WithEgressConfiguration(c => c.TemplatePort = ...)` | Optional; upstream default is `7980` |
| Debug handler port | `c.DebugHandlerPort` | Optional; `0` disables it upstream |
| PulseAudio sink reap grace period | `c.PulseSinkReapGraceSec` | Optional; upstream default is `30` seconds; negative disables reaping |
| Custom template base URL | `c.TemplateBase` | Optional; upstream default is `http://localhost:<template_port>/` |
| Chrome sandbox | `c.EnableChromeSandbox` | Optional; upstream default is `false` |
| Logging | `c.Logging` | Upstream logging options; JSON formatting is managed by Aspire |
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
| Cluster ID | `WithSipConfiguration(c => c.ClusterId = ...)` | Optional upstream setting |
| Maximum CPU utilization | `c.MaxCpuUtilization` | Optional upstream setting |
| Maximum active calls | `c.MaxActiveCalls` | Optional upstream setting |
| Signaling external IP / local network / NAT address | `c.UseExternalIp`, `c.LocalNet`, `c.Nat1To1Ip` | Optional upstream settings |
| Media external IP / NAT address | `c.MediaUseExternalIp`, `c.MediaNat1To1Ip` | Optional upstream settings |
| Jitter buffer | `c.EnableJitterBuffer`, `c.EnableJitterBufferProb` | Optional upstream settings |
| Logging | `c.Logging` | Upstream logging options; JSON formatting is managed by Aspire |
| JSON logs / structured logs | `WithJsonLogging()` / `WithStructuredLogs()` | JSON output is opt-in |
| Additional YAML | `WithSipAdditionalConfiguration(yaml)` | Adds upstream settings not managed by the integration |

The [LiveKit SIP service configuration](https://github.com/livekit/sip#config)
documents SIP YAML options supported by the selected image version. SIP trunks
and dispatch rules are separate LiveKit API resources; adding the SIP container
does not create them.
