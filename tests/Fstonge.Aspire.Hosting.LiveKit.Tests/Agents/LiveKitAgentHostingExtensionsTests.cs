using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Agents;

public sealed class LiveKitAgentHostingExtensionsTests
{
    [Fact]
    public async Task WithLiveKitAgentReference_AddsCredentialsNameAndDevelopmentFlag()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKitServer("livekit");
        var agent = builder.AddExecutable("agent", "python", ".", "agent.py")
            .WithLiveKitAgentReference(livekit, "voice-agent");

        var environment = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(agent.Resource);

        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_URL");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_WS_URL");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_API_KEY");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_API_SECRET");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_AGENT_NAME" && pair.Value == "voice-agent");
        if (builder.Environment.IsDevelopment())
        {
            Assert.Contains(environment, pair => pair.Key == "LIVEKIT_AGENT_DEV" && pair.Value == "true");
        }
    }

    [Fact]
    public async Task WithLiveKitAgentReference_AddsDevelopmentFlagInDevelopmentEnvironment()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        var livekit = builder.AddLiveKitServer("livekit");
        var agent = builder.AddExecutable("agent", "python", ".")
            .WithLiveKitAgentReference(livekit);

        var environment = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(agent.Resource);

        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_AGENT_DEV" && pair.Value == "true");
    }

    [Fact]
    public void AddPythonNodeNpmAndDockerAgents_AddParentRelationships()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKitServer("livekit");

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
                    && string.Equals(relationship.Type, "Parent", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void AddPythonAgent_ResolvesVirtualEnvironmentAndSystemExecutables()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var windowsProject = Path.Combine(root, "windows");
        var unixProject = Path.Combine(root, "unix");
        var systemProject = Path.Combine(root, "system");
        Directory.CreateDirectory(Path.Combine(windowsProject, ".venv", "Scripts"));
        Directory.CreateDirectory(Path.Combine(unixProject, ".venv", "bin"));
        Directory.CreateDirectory(systemProject);
        File.WriteAllText(Path.Combine(windowsProject, ".venv", "Scripts", "python.exe"), string.Empty);
        File.WriteAllText(Path.Combine(unixProject, ".venv", "bin", "python"), string.Empty);

        try
        {
            var livekit = DistributedApplicationBuilderTestHost.CreateBuilder().AddLiveKitServer("livekit");

            Assert.Equal(
                Path.Combine(windowsProject, ".venv", "Scripts", "python.exe"),
                livekit.AddPythonAgent("windows-agent", windowsProject).Resource.Command);
            Assert.Equal(
                Path.Combine(unixProject, ".venv", "bin", "python"),
                livekit.AddPythonAgent("unix-agent", unixProject).Resource.Command);
            Assert.Equal("python", livekit.AddPythonAgent("system-agent", systemProject).Resource.Command);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ProviderKeyHelpers_AddProviderEnvironmentVariables()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var openaiKey = builder.AddParameter("openai-key", "openai-secret", secret: true);
        var deepgramKey = builder.AddParameter("deepgram-key", "deepgram-secret", secret: true);
        var cartesiaKey = builder.AddParameter("cartesia-key", "cartesia-secret", secret: true);
        var elevenLabsKey = builder.AddParameter("elevenlabs-key", "elevenlabs-secret", secret: true);
        var anthropicKey = builder.AddParameter("anthropic-key", "anthropic-secret", secret: true);
        var agent = builder.AddExecutable("agent", "node", ".")
            .WithOpenAiApiKey(openaiKey)
            .WithDeepgramApiKey("deepgram-literal")
            .WithCartesiaApiKey(cartesiaKey)
            .WithElevenLabsApiKey("elevenlabs-literal")
            .WithAnthropicApiKey(anthropicKey);
        var overloadAgent = builder.AddExecutable("overload-agent", "node", ".")
            .WithOpenAiApiKey("openai-literal")
            .WithDeepgramApiKey(deepgramKey)
            .WithCartesiaApiKey("cartesia-literal")
            .WithElevenLabsApiKey(elevenLabsKey)
            .WithAnthropicApiKey("anthropic-literal");
        var environment = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(agent.Resource);
        var overloadEnvironment = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(overloadAgent.Resource);

        Assert.Contains(environment, pair => pair.Key == "OPENAI_API_KEY" && pair.Value == "{openai-key.value}");
        Assert.Contains(environment, pair => pair.Key == "DEEPGRAM_API_KEY" && pair.Value == "deepgram-literal");
        Assert.Contains(environment, pair => pair.Key == "CARTESIA_API_KEY" && pair.Value == "{cartesia-key.value}");
        Assert.Contains(environment, pair => pair.Key == "ELEVEN_API_KEY" && pair.Value == "elevenlabs-literal");
        Assert.Contains(environment, pair => pair.Key == "ANTHROPIC_API_KEY" && pair.Value == "{anthropic-key.value}");
        Assert.Contains(overloadEnvironment, pair => pair.Key == "OPENAI_API_KEY" && pair.Value == "openai-literal");
        Assert.Contains(overloadEnvironment, pair => pair.Key == "DEEPGRAM_API_KEY" && pair.Value == "{deepgram-key.value}");
        Assert.Contains(overloadEnvironment, pair => pair.Key == "CARTESIA_API_KEY" && pair.Value == "cartesia-literal");
        Assert.Contains(overloadEnvironment, pair => pair.Key == "ELEVEN_API_KEY" && pair.Value == "{elevenlabs-key.value}");
        Assert.Contains(overloadEnvironment, pair => pair.Key == "ANTHROPIC_API_KEY" && pair.Value == "anthropic-literal");
    }

    private static void AssertParent(IResource resource, IResource parent)
    {
        Assert.Contains(
            resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            relationship => ReferenceEquals(relationship.Resource, parent)
                && string.Equals(relationship.Type, "Parent", StringComparison.OrdinalIgnoreCase));
    }
}
