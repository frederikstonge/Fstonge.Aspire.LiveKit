# LiveKit Agent Example (Node.js)

This example demonstrates how to build and run an AI agent using LiveKit's Node.js Agent Framework (`@livekit/agents`) and orchestrate it through .NET Aspire.

The agent initializes LiveKit's Pino logger with pretty output disabled, so its
logs are emitted as JSON lines. Numeric Pino levels are mapped by the Aspire
log forwarder (for example, `30` is Information and `40` is Warning).

## Setup

1. Install dependencies:
   ```bash
   npm install
   ```

2. Run in development mode:
   ```bash
   npm run dev
   ```

## Aspire Orchestration

In your `AppHost.cs`:

```csharp
var openAiKey = builder.AddParameter("openai-api-key", secret: true);

var agent = livekit.AddNpmAgent("node-agent", "../Fstonge.Aspire.LiveKit.Example.NodeAgent")
    .WithOpenAiApiKey(openAiKey);
```
