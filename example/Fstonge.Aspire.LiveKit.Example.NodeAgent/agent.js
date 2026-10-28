import { cli, WorkerOptions, defineAgent } from '@livekit/agents';
import * as openai from '@livekit/agents-plugin-openai';
import dotenv from 'dotenv';

dotenv.config();

export default defineAgent({
  entry: async (ctx) => {
    await ctx.connect();
    console.log(`Node Agent connected to room: ${ctx.room.name}`);

    const participant = await ctx.waitForParticipant();
    console.log(`Starting voice session for participant: ${participant.identity}`);

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

