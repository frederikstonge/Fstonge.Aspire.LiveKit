# Fstonge.Aspire.Hosting.LiveKit

Adds a self-hosted LiveKit server and optional Ingress, Egress, and SIP
containers to a .NET Aspire AppHost. See the repository's

Use `.WithStructuredLogs()` to forward LiveKit's JSON console records from
Aspire's resource log stream to the dashboard's OTLP endpoint:

```csharp
var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis)
    .WithStructuredLogs();
```

The AppHost must have an OTLP endpoint configured through
`ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL`, `OTEL_EXPORTER_OTLP_ENDPOINT`, or
`OTEL_EXPORTER_OTLP_LOGS_ENDPOINT`. LiveKit logs remain available in the
resource console output. Non-JSON log entries are reported by the AppHost and
are not forwarded as structured logs. See the repository's
[getting-started guide](../../docs/getting-started.md) for shared Redis setup,
secret parameters, storage configuration, examples, and networking limits.
The package targets .NET 10.

The server API key and secret are generated as secret Aspire parameters when
you do not provide parameter resources. During local runs, Aspire persists the
generated values in the AppHost project's user secrets. They are available to
consuming projects only when `WithLiveKitReference` is called with
`includeCredentials: true`.

Egress and SIP are independently opt-in and require the server's Redis
resource. Egress output targets are supplied to its recording/streaming
requests or configured storage; a local output directory must be mounted into
the container. SIP signaling and RTP ports require suitable external routing,
which Aspire's local bindings do not provision.
