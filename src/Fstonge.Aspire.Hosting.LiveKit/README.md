# Fstonge.Aspire.Hosting.LiveKit

Adds a self-hosted LiveKit server and optional Ingress, Egress, and SIP
containers to a .NET Aspire AppHost. See the repository's
[getting-started guide](../../docs/getting-started.md) for shared Redis setup,
secret parameters, storage configuration, examples, and networking limits.

The server API key and secret are configured by the AppHost author through
secret Aspire parameters; LiveKit does not issue them automatically. They are
available to consuming projects only when `WithLiveKitReference` is called
with `includeCredentials: true`.

Egress and SIP are independently opt-in and require the server's Redis
resource. Egress output targets are supplied to its recording/streaming
requests or configured storage; a local output directory must be mounted into
the container. SIP signaling and RTP ports require suitable external routing,
which Aspire's local bindings do not provision.
