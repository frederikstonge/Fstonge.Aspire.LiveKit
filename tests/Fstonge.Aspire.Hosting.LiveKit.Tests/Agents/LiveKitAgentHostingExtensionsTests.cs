using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Agents;

[Collection(DistributedApplicationBuilderTestCollection.Name)]
public sealed class LiveKitAgentHostingExtensionsTests(DistributedApplicationBuilderTestHost appHost)
{
    [Fact]
    public async Task WithLiveKitAgentReference_AddsCredentialsNameAndDevelopmentFlag()
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit");
        var agent = builder.AddExecutable("agent", "python", ".", "agent.py")
            .WithLiveKitAgentReference(livekit, "voice-agent");

        var environment = await appHost.BuildEnvironmentAsync(agent.Resource);

        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_URL");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_API_KEY");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_API_SECRET");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_AGENT_NAME" && pair.Value == "voice-agent");
        if (builder.Environment.IsDevelopment())
        {
            Assert.Contains(environment, pair => pair.Key == "LIVEKIT_AGENT_DEV" && pair.Value == "true");
        }
    }

    [Fact]
    public void AddPythonNodeNpmAndDockerAgents_AddParentRelationships()
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit");

        var python = livekit.AddPythonAgent("python-agent", ".", "main.py", "python3", ["--verbose"]);
        var node = livekit.AddNodeAgent("node-agent", ".", "agent.js", "nodejs", ["--verbose"]);
        var npm = livekit.AddNpmAgent("npm-agent", ".", "serve", ["--verbose"]);
        var docker = livekit.AddAgentDockerfile("docker-agent", ".", dockerfilePath: "Dockerfile", stage: "runtime");

        AssertParent(python.Resource, livekit.Resource);
        AssertParent(node.Resource, livekit.Resource);
        AssertParent(npm.Resource, livekit.Resource);
        AssertParent(docker.Resource, livekit.Resource);
        Assert.All(
            builder.Resources.Where(resource => resource.Name.EndsWith("-agent", StringComparison.Ordinal)),
            resource => Assert.Contains(
                resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
                relationship => ReferenceEquals(relationship.Resource, livekit.Resource)
                    && relationship.Type == "parent"));
    }

    [Fact]
    public async Task ProviderKeyHelpers_AddProviderEnvironmentVariables()
    {
        var builder = appHost.CreateBuilder();
        var openaiKey = builder.AddParameter("openai-key", "openai-secret", secret: true);
        var cartesiaKey = builder.AddParameter("cartesia-key", "cartesia-secret", secret: true);
        var anthropicKey = builder.AddParameter("anthropic-key", "anthropic-secret", secret: true);
        var agent = builder.AddExecutable("agent", "node", ".")
            .WithOpenAiApiKey(openaiKey)
            .WithDeepgramApiKey("deepgram-literal")
            .WithCartesiaApiKey(cartesiaKey)
            .WithElevenLabsApiKey("elevenlabs-literal")
            .WithAnthropicApiKey(anthropicKey);
        var environment = await appHost.BuildEnvironmentAsync(agent.Resource);

        Assert.Contains(environment, pair => pair.Key == "OPENAI_API_KEY" && pair.Value == "openai-secret");
        Assert.Contains(environment, pair => pair.Key == "DEEPGRAM_API_KEY" && pair.Value == "deepgram-literal");
        Assert.Contains(environment, pair => pair.Key == "CARTESIA_API_KEY" && pair.Value == "cartesia-secret");
        Assert.Contains(environment, pair => pair.Key == "ELEVEN_API_KEY" && pair.Value == "elevenlabs-literal");
        Assert.Contains(environment, pair => pair.Key == "ANTHROPIC_API_KEY" && pair.Value == "anthropic-secret");
    }

    private static void AssertParent(IResource resource, IResource parent)
    {
        Assert.Contains(
            resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            relationship => ReferenceEquals(relationship.Resource, parent)
                && relationship.Type == "parent");
    }
}
