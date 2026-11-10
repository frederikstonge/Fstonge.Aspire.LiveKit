import { fileURLToPath } from 'node:url';
import { cli, ServerOptions, defineAgent, initializeLogger, log, voice } from '@livekit/agents';
import * as openai from '@livekit/agents-plugin-openai';
import dotenv from 'dotenv';

dotenv.config();

export default defineAgent({
  entry: async (ctx) => {
    initializeLogger({ pretty: false, level: 'info' });

    await ctx.connect();
    log().info({ room: ctx.room.name }, 'Node Agent connected to room');

    const agent = new voice.Agent({
      instructions:
        'You are a friendly and helpful voice AI assistant powered by LiveKit and .NET Aspire. ' +
        'Your output will be converted to speech. Keep responses concise, warm, and natural.',
    });

    const session = new voice.AgentSession({
      llm: new openai.LLM({ model: 'gpt-4o-mini' }),
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

cli.runApp(
  new ServerOptions({
    agent: fileURLToPath(import.meta.url),
    agentName: 'node-voice-agent',
  }),
);
