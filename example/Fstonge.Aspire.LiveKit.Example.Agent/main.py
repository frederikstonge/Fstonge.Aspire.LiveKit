import logging
from dotenv import load_dotenv

from livekit.agents import (
    AutoSubscribe,
    Agent,
    AgentSession,
    JobContext,
    JobProcess,
    WorkerOptions,
    cli,
)
from livekit.plugins import openai, silero

load_dotenv()
logger = logging.getLogger("livekit-voice-agent")


class VoiceAssistant(Agent):
    def __init__(self):
        super().__init__(
            instructions=(
                "You are a friendly and helpful voice AI assistant powered by LiveKit and .NET Aspire. "
                "Your output will be converted to speech. Keep responses concise, warm, and natural."
            )
        )


def prewarm(proc: JobProcess):
    """Preload models before handling incoming jobs for reduced latency."""
    proc.userdata["vad"] = silero.VAD.load()


async def entrypoint(ctx: JobContext):
    """Main worker entrypoint executed when a job is assigned to this worker."""
    logger.info(f"Connecting to room: {ctx.room.name}")
    await ctx.connect(auto_subscribe=AutoSubscribe.AUDIO_ONLY)

    # Wait for the first human participant to join the room
    participant = await ctx.wait_for_participant()
    logger.info(f"Starting voice assistant for participant: {participant.identity}")

    session = AgentSession(
        vad=ctx.proc.userdata["vad"],
        stt=openai.STT(),
        llm=openai.LLM(model="gpt-4o-mini"),
        tts=openai.TTS(),
    )

    await session.start(room=ctx.room, agent=VoiceAssistant())
    await session.say("Hello! How can I help you today?", allow_interruptions=True)


if __name__ == "__main__":
    cli.run_app(WorkerOptions(entrypoint_fnc=entrypoint, prewarm_fnc=prewarm))
