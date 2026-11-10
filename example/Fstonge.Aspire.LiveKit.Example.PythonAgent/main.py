import logging
from dotenv import load_dotenv

from livekit import agents
from livekit.agents import (
    Agent,
    AgentServer,
    AgentSession,
    JobContext,
    JobProcess,
    cli,
)
from livekit.plugins import openai, silero

load_dotenv()
logger = logging.getLogger("livekit-voice-agent")

server = AgentServer()


def prewarm(proc: JobProcess):
    """Preload models before handling incoming jobs for reduced latency."""
    proc.userdata["vad"] = silero.VAD.load()


server.setup_fnc = prewarm


@server.rtc_session(agent_name="python-voice-agent")
async def entrypoint(ctx: JobContext):
    """Main worker entrypoint executed when a job is assigned to this worker."""
    logger.info(f"Connecting to room: {ctx.room.name}")
    await ctx.connect()

    session = AgentSession(
        vad=ctx.proc.userdata.get("vad") or silero.VAD.load(),
        stt=openai.STT(),
        llm=openai.LLM(model="gpt-4o-mini"),
        tts=openai.TTS(),
    )

    agent = Agent(
        instructions=(
            "You are a friendly and helpful voice AI assistant powered by LiveKit and .NET Aspire. "
            "Your output will be converted to speech. Keep responses concise, warm, and natural."
        )
    )

    await session.start(room=ctx.room, agent=agent)
    await session.generate_reply(instructions="Greet the user and offer your assistance.")


if __name__ == "__main__":
    cli.run_app(server)
