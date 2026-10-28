# LiveKit Voice Agent Example (Python)

This example demonstrates how to build and run an AI voice assistant using LiveKit's Agent Framework and orchestrate it through .NET Aspire.

## Features
- **Voice Pipeline**: Real-time conversational AI combining Silero VAD (Voice Activity Detection), OpenAI Speech-to-Text (STT), OpenAI GPT-4o-mini (LLM), and OpenAI Text-to-Speech (TTS).
- **Auto-Subscription**: Subscribes to participant audio tracks and streams synthetic audio back into the room.
- **Aspire Integration**: Automatically receives `LIVEKIT_URL`, `LIVEKIT_API_KEY`, and `LIVEKIT_API_SECRET` from the parent LiveKit server resource in the Aspire AppHost.

## Local Setup (Independent Run)

1. Create and activate a Python virtual environment:
   ```bash
   python -m venv .venv
   # Windows:
   .venv\Scripts\activate
   # Linux/macOS:
   source .venv/bin/activate
   ```

2. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```

3. Configure environment variables (or provide via Aspire AppHost):
   ```bash
   LIVEKIT_URL=http://localhost:7880
   LIVEKIT_API_KEY=your-api-key
   LIVEKIT_API_SECRET=your-api-secret
   OPENAI_API_KEY=your-openai-api-key
   ```

4. Run the agent worker in development mode:
   ```bash
   python main.py dev
   ```

## Aspire Orchestration

In your `AppHost.cs`:

```csharp
var openAiKey = builder.AddParameter("openai-api-key", secret: true);

var agent = livekit.AddPythonAgent("voice-agent", "../Fstonge.Aspire.LiveKit.Example.PythonAgent", "main.py")
    .WithOpenAiApiKey(openAiKey);
```

