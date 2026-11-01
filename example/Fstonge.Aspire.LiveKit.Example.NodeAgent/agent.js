import { fileURLToPath } from 'node:url';
import { cli, WorkerOptions, defineAgent, initializeLogger, log } from '@livekit/agents';
import * as openai from '@livekit/agents-plugin-openai';
import dotenv from 'dotenv';

dotenv.config();

export default defineAgent({
  entry: async (ctx) => {
    initializeLogger({ pretty: false, level: 'info' });

    await ctx.connect();
    log().info({ room: ctx.room.name }, 'Node Agent connected to room');

    const participant = await ctx.waitForParticipant();
    log().info({ participant: participant.identity }, 'Starting voice session for participant');

    // Set up voice pipeline agent with OpenAI
    const voice = new openai.VoicePipelineAgent({
      chatCtx: {
        messages: [
          {
            role: 'system',
            content: 'You are a helpful voice assistant orchestrated with .NET Aspire and LiveKit.',
          },
        ],
      },
    });

    await voice.start(ctx.room, participant);
    await voice.say('Hello from the LiveKit Node.js agent! How can I assist you today?');
  },
});

cli.runApp(new WorkerOptions({ agent: fileURLToPath(import.meta.url) }));
