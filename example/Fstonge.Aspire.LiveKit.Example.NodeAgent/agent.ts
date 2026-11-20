import { fileURLToPath } from 'node:url';
import {
  cli,
  ServerOptions,
  defineAgent,
  initializeLogger,
  log,
  voice,
  type VAD,
} from '@livekit/agents';
import * as openai from '@livekit/agents-plugin-openai';
import * as silero from '@livekit/agents-plugin-silero';
import dotenv from 'dotenv';

type ProcessUserData = {
  vad?: VAD;
};

dotenv.config();
initializeLogger({ pretty: false, level: 'info' });

const agentDefinition = defineAgent<ProcessUserData>({
  prewarm: async (proc) => {
    proc.userData.vad = await silero.VAD.load();
  },
  entry: async (ctx) => {
    log().info({ room: ctx.room.name }, 'Node Agent connected to room');
    await ctx.connect();

    const session = new voice.AgentSession({
      vad: ctx.proc.userData.vad ?? await silero.VAD.load(),
      stt: new openai.STT(),
      llm: new openai.LLM({ model: 'gpt-4o-mini' }),
      tts: new openai.TTS(),
    });

    const agent = new voice.Agent({
      instructions:
        'You are a friendly and helpful voice AI assistant powered by LiveKit and .NET Aspire. ' +
        'Your output will be converted to speech. Keep responses concise, warm, and natural.',
    });

    await session.start({
      agent,
      room: ctx.room,
    });

    await session.generateReply({
      instructions: 'Greet the user and offer your assistance.',
    });
  },
});

export default agentDefinition;

cli.runApp(
  new ServerOptions({
    agent: fileURLToPath(import.meta.url),
    agentName: 'node-voice-agent',
  }),
);
