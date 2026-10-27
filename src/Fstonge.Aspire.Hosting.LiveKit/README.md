# Fstonge.Aspire.Hosting.LiveKit

Adds a self-hosted LiveKit server and an optional Ingress container to an
.NET Aspire AppHost. See the repository's [getting-started guide](../../docs/getting-started.md)
for secret parameter setup, examples, and networking limitations.

The server API key and secret are configured by the AppHost author through
secret Aspire parameters; LiveKit does not issue them automatically. They are
available to consuming projects only when `WithLiveKitReference` is called
with `includeCredentials: true`.
