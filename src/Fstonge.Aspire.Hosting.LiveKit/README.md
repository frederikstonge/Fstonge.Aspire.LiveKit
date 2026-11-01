# Fstonge.Aspire.Hosting.LiveKit

Adds a self-hosted LiveKit server and optional Ingress, Egress, and SIP
containers to a .NET Aspire AppHost.

Ingress, Egress, and SIP resources appear nested under their LiveKit server in
the Aspire dashboard.

Use `.WithStructuredLogs()` on LiveKit, Ingress, Egress, or SIP resources to
forward logs from Aspire's resource log stream to the dashboard's OTLP
endpoint. The log forwarder supports all resource types in this integration,
including executable and containerized Python and Node.js agents:

```csharp
var livekit = builder.AddLiveKitServer("livekit")
    .WithLiveKitServerRedis(redis)
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
`OTEL_EXPORTER_OTLP_LOGS_ENDPOINT`. LiveKit server JSON and tab-delimited
records, Python agent JSON records, and other object-shaped JSON records are
forwarded with their parsed fields. Plain-text output and malformed or
unsupported JSON remain available as message-only logs. Logs remain available
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
`WithLiveKitServerAdditionalConfiguration`, `WithIngressAdditionalConfiguration`,
`WithEgressAdditionalConfiguration`, and `WithSipAdditionalConfiguration` for
additional top-level YAML settings supported by the selected service version.
See the repository's
[getting-started guide](../../docs/getting-started.md) for examples and the
[complete image configuration reference](../../docs/image-configuration.md).

Server JSON console logging is enabled by default. Call `.WithJsonLogging()` on
Ingress, Egress, or SIP to enable JSON console output without OTLP forwarding.
Aspire captures console output for all resources. To also forward logs to the
dashboard's Structured Logs view over OTLP, call `.WithStructuredLogs()` on
the resource; this also enables JSON logging for LiveKit, Ingress, Egress, and
SIP resources. Agent resources are supported too. LiveKit server JSON and
tab-delimited records, Python agent JSON records, and other object-shaped JSON
records are forwarded with their parsed fields. Plain-text output and malformed
or unsupported JSON are forwarded as message-only logs. The AppHost must have
an OTLP endpoint configured.

YamlDotNet serializes typed options and parses extension YAML. Additional YAML
must be a single top-level mapping and cannot override settings managed by the
integration.

No API key or secret arguments are required when calling `AddLiveKitServer(name)`.
The server API key and secret are generated as secret Aspire parameters and,
during local runs, persisted in the AppHost project's user secrets. You can
optionally provide your own secret parameters. Credentials are available to
consuming projects only when `WithLiveKitServerReference` is called with
`includeCredentials: true`.

To connect to a LiveKit server managed outside the Aspire application, add an
external resource instead. This does not start a container; it makes the
external URL available to dependent resources. Supply secret parameters when
the application also needs API credentials:

```csharp
var livekitUrl = builder.AddParameter("livekit-url");
var livekitApiKey = builder.AddParameter("livekit-api-key", secret: true);
var livekitApiSecret = builder.AddParameter("livekit-api-secret", secret: true);
var livekit = builder.AddExternalLiveKitServer(
    "livekit",
    livekitUrl,
    livekitApiKey,
    livekitApiSecret);

var api = builder.AddProject<Projects.Api>("api")
    .WithLiveKitServerReference(livekit, includeCredentials: true);
```

Configure `livekit-url` as an absolute HTTP or HTTPS URL using your AppHost's
parameter configuration. API credentials are optional unless the consumer
needs them; both key and secret must be supplied together. The external
resource does not provision, monitor, or configure networking for the LiveKit
deployment.

Egress and SIP are independently opt-in and require the server's Redis
resource. Egress output targets are supplied to its recording/streaming
requests or configured storage; a local output directory must be mounted into
the container. SIP signaling and RTP ports require suitable external routing,
which Aspire's local bindings do not provision.

## TypeScript AppHosts

This package exports its resource types and supported APIs through Aspire's
Type System, so the Aspire CLI can generate a typed SDK for TypeScript AppHosts.
Add the integration and restore the AppHost to generate its SDK:

```sh
aspire add Fstonge.Aspire.Hosting.LiveKit
aspire restore
```

The generated SDK exposes LiveKit server creation and external LiveKit
references, Redis configuration,
Ingress/Egress/SIP creation, TURN and SIP TLS settings, YAML configuration,
Egress storage, logging, and the Python, Node.js, npm, and Dockerfile agent
helpers. For example:

```typescript
import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();
const redis = await builder.addRedis("redis");
const livekit = await builder.addLiveKit("livekit");

await livekit.withRedis(redis);
const egress = await livekit.addEgress("egress", redis);
await egress.withEgressAdditionalConfiguration("logging:\n  level: info\n");

await builder.build().run();
```

The extension also keeps C#-specific callback configuration overloads for
C# AppHosts. Generated AppHosts can use the corresponding additional-YAML
methods instead.

## LiveKit Agent Framework Integration

You can easily orchestrate LiveKit AI Agents (Python or Node.js/npm) directly alongside the LiveKit server:

```csharp
var openAiKey = builder.AddParameter("openai-api-key", secret: true);

// Python Agent
var pythonAgent = livekit.AddPythonAgent(
    "voice-agent",
    projectDirectory: "../agents/voice_agent",
    scriptPath: "main.py")
    .WithOpenAiApiKey(openAiKey);

// Node.js / npm Agent
var nodeAgent = livekit.AddNpmAgent(
    "node-agent",
    projectDirectory: "../agents/node_agent")
    .WithOpenAiApiKey(openAiKey);

// Containerized Agent
var containerAgent = livekit.AddAgentDockerfile(
    "container-agent",
    contextPath: "../agents/voice_agent")
    .WithOpenAiApiKey(openAiKey);
```

### Generic Agent Binding
Any resource supporting environment variables can be configured as a LiveKit agent worker using `WithLiveKitAgentReference`:

```csharp
var customAgent = builder.AddExecutable("custom-agent", "python", "../agent", "main.py")
    .WithLiveKitAgentReference(livekit, agentName: "voice-assistant");
```

This automatically:
- Injects `LIVEKIT_URL`, `LIVEKIT_API_KEY`, and `LIVEKIT_API_SECRET`.
- Injects `LIVEKIT_AGENT_NAME` and sets `LIVEKIT_AGENT_DEV=true` in development environments (`builder.Environment.IsDevelopment()`).
- Waits for the LiveKit server to be healthy (`WaitFor(livekit)`).

`WithLiveKitAgentReference` does not change the resource's executable, command-line
arguments, or container command. Configure those for your worker explicitly.
The `AddPythonAgent`, `AddNodeAgent`, and `AddNpmAgent` helpers add their
development (`dev`) or production (`start`) command conventions; Dockerfile
agents use the command defined by the image.

### AI Provider Helpers
Fluent extensions are provided for common LLM, STT, and TTS providers:
- `WithOpenAiApiKey(...)`
- `WithDeepgramApiKey(...)`
- `WithCartesiaApiKey(...)`
- `WithElevenLabsApiKey(...)`
- `WithAnthropicApiKey(...)`
