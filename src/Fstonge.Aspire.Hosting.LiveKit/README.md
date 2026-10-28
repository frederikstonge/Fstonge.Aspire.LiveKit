# Fstonge.Aspire.Hosting.LiveKit

Adds a self-hosted LiveKit server and optional Ingress, Egress, and SIP
containers to a .NET Aspire AppHost.

Ingress, Egress, and SIP resources appear nested under their LiveKit server in
the Aspire dashboard.

Use `.WithStructuredLogs()` on any of these resources to forward its logs from
Aspire's resource log stream to the dashboard's OTLP endpoint:

```csharp
var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis)
    .WithStructuredLogs();

var ingress = livekit.AddIngress(
        "ingress",
        redis,
        "rtmp://localhost:1935/live",
        "http://localhost:8080/whip")
    .WithStructuredLogs();
```

The AppHost must have an OTLP endpoint configured through
`ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL`, `OTEL_EXPORTER_OTLP_ENDPOINT`, or
`OTEL_EXPORTER_OTLP_LOGS_ENDPOINT`. Server JSON records are forwarded with
their parsed fields. Companion-service JSON records are parsed when available;
plain-text records are forwarded as message-only logs. Logs remain available
in the resource console output. See the repository's
[getting-started guide](../../docs/getting-started.md) for shared Redis setup,
secret parameters, and examples; see the
[optional services guide](../../docs/optional-services.md) for storage
configuration and networking limits, and the
[complete image configuration reference](../../docs/image-configuration.md).
The package targets .NET 10.

Use `WithLiveKitServerConfiguration`, `WithIngressConfiguration`,
`WithEgressConfiguration`, or `WithSipConfiguration` with an `Action<T>`
callback to configure typed service options. Use
`WithLiveKitAdditionalConfiguration`, `WithIngressAdditionalConfiguration`,
`WithEgressAdditionalConfiguration`, and `WithSipAdditionalConfiguration` for
additional top-level YAML settings supported by the selected service version.
See the repository's
[getting-started guide](../../docs/getting-started.md) for examples and the
[complete image configuration reference](../../docs/image-configuration.md).

Server JSON console logging is enabled by default. Call `.WithJsonLogging()` on
Ingress, Egress, or SIP to enable JSON console output without OTLP forwarding.
Aspire captures console output for all resources. To also forward logs to the
dashboard's Structured Logs view over OTLP, call `.WithStructuredLogs()` on
the resource; this also enables JSON logging. JSON records are forwarded with
parsed fields when possible, and plain-text companion records are forwarded as
message-only logs. The AppHost must have an OTLP endpoint configured.

YamlDotNet serializes typed options and parses extension YAML. Additional YAML
must be a single top-level mapping and cannot override settings managed by the
integration.

No API key or secret arguments are required when calling `AddLiveKit(name)`.
The server API key and secret are generated as secret Aspire parameters and,
during local runs, persisted in the AppHost project's user secrets. You can
optionally provide your own secret parameters. Credentials are available to
consuming projects only when `WithLiveKitReference` is called with
`includeCredentials: true`.

Egress and SIP are independently opt-in and require the server's Redis
resource. Egress output targets are supplied to its recording/streaming
requests or configured storage; a local output directory must be mounted into
the container. SIP signaling and RTP ports require suitable external routing,
which Aspire's local bindings do not provision.
