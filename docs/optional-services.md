# Optional services

Ingress, Egress, and SIP are optional resources. Each requires the same Redis
resource used by LiveKit and starts only when explicitly added. In the Aspire
dashboard, these resources appear nested under their LiveKit server.

For the core setup, see the [getting-started guide](getting-started.md). For
all settings exposed by the package, see the
[image configuration reference](image-configuration.md).

## Ingress

Provide externally reachable RTMP and WHIP URLs:

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

## Egress

Egress is independent from Ingress and starts only when added:

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

## SIP

SIP is independent from both Ingress and Egress:

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
