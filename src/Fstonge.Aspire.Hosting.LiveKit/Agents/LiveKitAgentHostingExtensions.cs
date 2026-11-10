using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for registering and configuring LiveKit Agent Framework workers with .NET Aspire.
/// </summary>
public static class LiveKitAgentHostingExtensions
{
    /// <summary>
    /// Configures a resource to connect to LiveKit as an Agent Framework worker by injecting
    /// <c>LIVEKIT_URL</c>, <c>LIVEKIT_WS_URL</c>, <c>LIVEKIT_API_KEY</c>, and <c>LIVEKIT_API_SECRET</c>, and waiting for the LiveKit server.
    /// In development environments, also sets <c>LIVEKIT_AGENT_DEV</c> to <c>"true"</c>.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The agent resource builder.</param>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="agentName">An optional name identifying this agent worker.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "The method accepts arbitrary C# resource types; generated AppHosts can configure environment variables directly.")]
    public static IResourceBuilder<T> WithLiveKitAgentReference<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<LiveKitServerResource> livekit,
        string? agentName = null)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(livekit);

        builder.WithEnvironment("LIVEKIT_URL", livekit.GetEndpoint("http"));
        builder.WithEnvironment("LIVEKIT_WS_URL", livekit.GetEndpoint("ws"));
        builder.WithEnvironment("LIVEKIT_API_KEY", livekit.Resource.ApiKey);
        builder.WithEnvironment("LIVEKIT_API_SECRET", livekit.Resource.ApiSecret);

        if (!string.IsNullOrWhiteSpace(agentName))
        {
            builder.WithEnvironment("LIVEKIT_AGENT_NAME", agentName);
        }

        if (builder.ApplicationBuilder.Environment.IsDevelopment())
        {
            builder.WithEnvironment("LIVEKIT_AGENT_DEV", "true");
        }

        if (builder.Resource is IResourceWithWaitSupport waitSupportResource)
        {
            var waitBuilder = builder.ApplicationBuilder.CreateResourceBuilder<IResourceWithWaitSupport>(waitSupportResource);
            waitBuilder.WaitFor(livekit.ApplicationBuilder.CreateResourceBuilder<IResource>(livekit.Resource));
        }

        return builder;
    }

    /// <summary>
    /// Adds a Python-based LiveKit Agent worker to the distributed application.
    /// Runs with the <c>"dev"</c> CLI command in development environments, or <c>"start"</c> otherwise.
    /// </summary>
    /// <param name="livekit">The parent LiveKit server resource builder.</param>
    /// <param name="name">The name of the agent resource.</param>
    /// <param name="projectDirectory">The path to the Python project directory.</param>
    /// <param name="scriptPath">The Python script entry point. Defaults to <c>"main.py"</c>.</param>
    /// <param name="pythonExecutable">The optional Python binary to use. Defaults to resolving virtual environment or system <c>"python"</c>.</param>
    /// <param name="additionalArgs">Optional additional command-line arguments to pass to the script.</param>
    /// <returns>An <see cref="IResourceBuilder{ExecutableResource}"/> for configuring the Python agent worker.</returns>
    [AspireExport]
    public static IResourceBuilder<ExecutableResource> AddPythonAgent(
        this IResourceBuilder<LiveKitServerResource> livekit,
        [ResourceName] string name,
        string projectDirectory,
        string scriptPath = "main.py",
        string? pythonExecutable = null,
        string[]? additionalArgs = null)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);

        var isDevelopment = livekit.ApplicationBuilder.Environment.IsDevelopment();
        var python = pythonExecutable ?? ResolvePythonExecutable(projectDirectory);
        var commandArgs = new List<string> { scriptPath, isDevelopment ? "dev" : "start" };

        if (additionalArgs is { Length: > 0 })
        {
            commandArgs.AddRange(additionalArgs);
        }

        var agent = livekit.ApplicationBuilder
            .AddExecutable(name, python, projectDirectory, commandArgs.ToArray())
            .WithParentRelationship(livekit.Resource)
            .WithLiveKitAgentReference(livekit, agentName: name);

        return agent;
    }

    /// <summary>
    /// Adds a Node.js-based LiveKit Agent worker to the distributed application.
    /// Runs with the <c>"dev"</c> CLI command in development environments, or <c>"start"</c> otherwise.
    /// </summary>
    /// <param name="livekit">The parent LiveKit server resource builder.</param>
    /// <param name="name">The name of the agent resource.</param>
    /// <param name="projectDirectory">The path to the Node.js project directory.</param>
    /// <param name="scriptPath">The Node.js script entry point. Defaults to <c>"agent.js"</c>.</param>
    /// <param name="nodeExecutable">The optional Node.js binary to use. Defaults to <c>"node"</c>.</param>
    /// <param name="additionalArgs">Optional additional command-line arguments to pass to the script.</param>
    /// <returns>An <see cref="IResourceBuilder{ExecutableResource}"/> for configuring the Node.js agent worker.</returns>
    [AspireExport]
    public static IResourceBuilder<ExecutableResource> AddNodeAgent(
        this IResourceBuilder<LiveKitServerResource> livekit,
        [ResourceName] string name,
        string projectDirectory,
        string scriptPath = "agent.js",
        string? nodeExecutable = null,
        string[]? additionalArgs = null)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);

        var isDevelopment = livekit.ApplicationBuilder.Environment.IsDevelopment();
        var node = nodeExecutable ?? "node";
        var commandArgs = new List<string> { scriptPath, isDevelopment ? "dev" : "start" };

        if (additionalArgs is { Length: > 0 })
        {
            commandArgs.AddRange(additionalArgs);
        }

        var agent = livekit.ApplicationBuilder
            .AddExecutable(name, node, projectDirectory, commandArgs.ToArray())
            .WithParentRelationship(livekit.Resource)
            .WithLiveKitAgentReference(livekit, agentName: name);

        return agent;
    }

    /// <summary>
    /// Adds an npm-based LiveKit Agent worker to the distributed application.
    /// Defaults to executing the <c>"dev"</c> script in development environments, or <c>"start"</c> otherwise.
    /// </summary>
    /// <param name="livekit">The parent LiveKit server resource builder.</param>
    /// <param name="name">The name of the agent resource.</param>
    /// <param name="projectDirectory">The path to the project directory containing package.json.</param>
    /// <param name="scriptName">The optional npm script name to execute. Defaults to <c>"dev"</c> in development, <c>"start"</c> otherwise.</param>
    /// <param name="additionalArgs">Optional additional command-line arguments to pass to npm.</param>
    /// <returns>An <see cref="IResourceBuilder{ExecutableResource}"/> for configuring the npm agent worker.</returns>
    [AspireExport]
    public static IResourceBuilder<ExecutableResource> AddNpmAgent(
        this IResourceBuilder<LiveKitServerResource> livekit,
        [ResourceName] string name,
        string projectDirectory,
        string? scriptName = null,
        string[]? additionalArgs = null)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);

        var isDevelopment = livekit.ApplicationBuilder.Environment.IsDevelopment();
        var script = scriptName ?? (isDevelopment ? "dev" : "start");
        var npm = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
        var commandArgs = new List<string> { "run", script };

        if (additionalArgs is { Length: > 0 })
        {
            commandArgs.Add("--");
            commandArgs.AddRange(additionalArgs);
        }

        var agent = livekit.ApplicationBuilder
            .AddExecutable(name, npm, projectDirectory, commandArgs.ToArray())
            .WithParentRelationship(livekit.Resource)
            .WithLiveKitAgentReference(livekit, agentName: name);

        return agent;
    }

    /// <summary>
    /// Adds a containerized LiveKit Agent worker built from a Dockerfile to the distributed application.
    /// </summary>
    /// <param name="livekit">The parent LiveKit server resource builder.</param>
    /// <param name="name">The name of the agent resource.</param>
    /// <param name="contextPath">The build context path containing the Dockerfile.</param>
    /// <param name="dockerfilePath">The optional path to the Dockerfile relative to <paramref name="contextPath"/>.</param>
    /// <param name="stage">The optional multi-stage build stage target.</param>
    /// <returns>An <see cref="IResourceBuilder{ContainerResource}"/> for configuring the containerized agent.</returns>
    [AspireExport]
    public static IResourceBuilder<ContainerResource> AddAgentDockerfile(
        this IResourceBuilder<LiveKitServerResource> livekit,
        [ResourceName] string name,
        string contextPath,
        string? dockerfilePath = null,
        string? stage = null)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextPath);

        var agent = livekit.ApplicationBuilder
            .AddDockerfile(name, contextPath, dockerfilePath, stage)
            .WithParentRelationship(livekit.Resource)
            .WithLiveKitAgentReference(livekit, agentName: name);

        return agent;
    }

    /// <summary>Enables OTLP log forwarding for a LiveKit Egress resource.</summary>
    /// <param name="resourceBuilder">The Egress resource.</param>
    /// <returns>The configured resource.</returns>
    [AspireExport]
    internal static IResourceBuilder<IResource> WithAgentStructuredLogs(
        IResourceBuilder<IResource> resourceBuilder) =>
        LiveKitServiceLoggingExtensions.WithAgentStructuredLogs(resourceBuilder);

    /// <summary>
    /// Injects an OpenAI API key parameter into the agent resource environment.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The parameter containing the OpenAI API key.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "Generated AppHosts can inject the parameter with their environment configuration APIs.")]
    public static IResourceBuilder<T> WithOpenAiApiKey<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<ParameterResource> apiKey)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiKey);
        return builder.WithEnvironment("OPENAI_API_KEY", apiKey);
    }

    /// <summary>
    /// Injects an OpenAI API key literal into the agent resource environment.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "Generated AppHosts can inject literal values with their environment configuration APIs.")]
    public static IResourceBuilder<T> WithOpenAiApiKey<T>(
        this IResourceBuilder<T> builder,
        string apiKey)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return builder.WithEnvironment("OPENAI_API_KEY", apiKey);
    }

    /// <summary>
    /// Injects a Deepgram API key parameter into the agent resource environment.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The parameter containing the Deepgram API key.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "Generated AppHosts can inject the parameter with their environment configuration APIs.")]
    public static IResourceBuilder<T> WithDeepgramApiKey<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<ParameterResource> apiKey)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiKey);
        return builder.WithEnvironment("DEEPGRAM_API_KEY", apiKey);
    }

    /// <summary>
    /// Injects a Deepgram API key literal into the agent resource environment.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The Deepgram API key.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "Generated AppHosts can inject literal values with their environment configuration APIs.")]
    public static IResourceBuilder<T> WithDeepgramApiKey<T>(
        this IResourceBuilder<T> builder,
        string apiKey)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return builder.WithEnvironment("DEEPGRAM_API_KEY", apiKey);
    }

    /// <summary>
    /// Injects a Cartesia API key parameter into the agent resource environment.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The parameter containing the Cartesia API key.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "Generated AppHosts can inject the parameter with their environment configuration APIs.")]
    public static IResourceBuilder<T> WithCartesiaApiKey<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<ParameterResource> apiKey)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiKey);
        return builder.WithEnvironment("CARTESIA_API_KEY", apiKey);
    }

    /// <summary>
    /// Injects a Cartesia API key literal into the agent resource environment.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The Cartesia API key.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "Generated AppHosts can inject literal values with their environment configuration APIs.")]
    public static IResourceBuilder<T> WithCartesiaApiKey<T>(
        this IResourceBuilder<T> builder,
        string apiKey)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return builder.WithEnvironment("CARTESIA_API_KEY", apiKey);
    }

    /// <summary>
    /// Injects an ElevenLabs API key parameter into the agent resource environment.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The parameter containing the ElevenLabs API key.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "Generated AppHosts can inject the parameter with their environment configuration APIs.")]
    public static IResourceBuilder<T> WithElevenLabsApiKey<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<ParameterResource> apiKey)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiKey);
        return builder.WithEnvironment("ELEVEN_API_KEY", apiKey);
    }

    /// <summary>
    /// Injects an ElevenLabs API key literal into the agent resource environment.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The ElevenLabs API key.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "Generated AppHosts can inject literal values with their environment configuration APIs.")]
    public static IResourceBuilder<T> WithElevenLabsApiKey<T>(
        this IResourceBuilder<T> builder,
        string apiKey)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return builder.WithEnvironment("ELEVEN_API_KEY", apiKey);
    }

    /// <summary>
    /// Injects an Anthropic API key parameter into the agent resource environment.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The parameter containing the Anthropic API key.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "Generated AppHosts can inject the parameter with their environment configuration APIs.")]
    public static IResourceBuilder<T> WithAnthropicApiKey<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<ParameterResource> apiKey)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiKey);
        return builder.WithEnvironment("ANTHROPIC_API_KEY", apiKey);
    }

    /// <summary>
    /// Injects an Anthropic API key literal into the agent resource environment.
    /// </summary>
    /// <typeparam name="T">The resource type supporting environment variables.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiKey">The Anthropic API key.</param>
    /// <returns>The updated resource builder.</returns>
    [AspireExportIgnore(Reason = "Generated AppHosts can inject literal values with their environment configuration APIs.")]
    public static IResourceBuilder<T> WithAnthropicApiKey<T>(
        this IResourceBuilder<T> builder,
        string apiKey)
        where T : IResource, IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return builder.WithEnvironment("ANTHROPIC_API_KEY", apiKey);
    }

    private static string ResolvePythonExecutable(string projectDirectory)
    {
        var venvWindows = Path.Combine(projectDirectory, ".venv", "Scripts", "python.exe");
        if (File.Exists(venvWindows))
        {
            return venvWindows;
        }

        var venvUnix = Path.Combine(projectDirectory, ".venv", "bin", "python");
        if (File.Exists(venvUnix))
        {
            return venvUnix;
        }

        return "python";
    }
}
