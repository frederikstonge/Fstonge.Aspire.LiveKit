using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

#pragma warning disable ASPIRERESOURCETRAITS001

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithTags("database");

var livekit = builder.AddLiveKitServer("livekit")
    .WithLiveKitServerRedis(cache)
    .WithStructuredLogs()
    .WithLiveKitServerConfiguration(config => 
    {
        config.LogLevel = "info";
    })
    .WithTags("livekit");

livekit.AddIngress(
    "ingress",
    cache,
    rtmpBaseUrl: "rtmp://localhost:1935/live",
    whipBaseUrl: "http://localhost:8080/whip")
    .WithStructuredLogs()
    .WithIngressConfiguration(config =>
    {
        config.Logging.Level = "info";
        config.CpuCost.RtmpCpuCost = 2;
        config.CpuCost.WhipCpuCost = 2;
        config.CpuCost.WhipBypassTranscodingCpuCost = 0.1;
        config.CpuCost.UrlCpuCost = 2;
    })
    .WithTags("livekit");

livekit.AddEgress(
    "egress",
    cache,
    healthPort: 8081,
    prometheusPort: 9090)
    .WithStructuredLogs()
    .WithEgressConfiguration(config =>
    {
        config.Logging.Level = "info";
    })
    .WithTags("livekit");

livekit.AddSip(
    "sip",
    cache,
    sipPort: 5060,
    rtpPortStart: 10000,
    rtpPortEnd: 20000,
    healthPort: 8082)
    .WithStructuredLogs()
    .WithSipConfiguration(config =>
    {
        config.Logging.Level = "info";
    })
    .WithTags("livekit");

var enablePythonAgent = builder.Configuration.GetValue<bool>("LiveKit:EnablePythonAgent", false);
var enableNodeAgent = builder.Configuration.GetValue<bool>("LiveKit:EnableNodeAgent", false);

if (enablePythonAgent || enableNodeAgent)
{
    var openAiKey = builder.AddParameter("openai-api-key", secret: true);
    var useDockerImage = builder.Configuration.GetValue<bool>("LiveKit:UseDockerImage", false);

    if (enablePythonAgent)
    {
        if (useDockerImage)
        {
            livekit.AddAgentDockerfile(
                "python-docker-voice-agent",
                "../Fstonge.Aspire.LiveKit.Example.PythonAgent",
                dockerfilePath: "Dockerfile")
                .WithOpenAiApiKey(openAiKey)
                .WithAgentStructuredLogs()
                .WithTags("agents");
        }
        else
        {
            livekit.AddPythonAgent(
            "python-voice-agent",
            "../Fstonge.Aspire.LiveKit.Example.PythonAgent",
            scriptPath: "main.py")
            .WithOpenAiApiKey(openAiKey)
            .WithAgentStructuredLogs()
            .WithTags("agents");
        }
    }

    if (enableNodeAgent)
    {
        if (useDockerImage)
        {
            livekit.AddAgentDockerfile(
                "node-docker-voice-agent",
                "../Fstonge.Aspire.LiveKit.Example.NodeAgent",
                dockerfilePath: "Dockerfile")
                .WithOpenAiApiKey(openAiKey)
                .WithAgentStructuredLogs()
                .WithTags("agents");
        }
        else
        {
            livekit.AddNodeAgent(
                "node-voice-agent",
                "../Fstonge.Aspire.LiveKit.Example.NodeAgent",
                scriptPath: "agent.js")
                .WithOpenAiApiKey(openAiKey)
                .WithAgentStructuredLogs()
                .WithTags("agents");
        }
    }
}

var apiService = builder.AddDotnetProject(
    "apiservice", 
    "../Fstonge.Aspire.LiveKit.Example.ApiService/Fstonge.Aspire.LiveKit.Example.ApiService.csproj",
    options => options.LaunchProfileName = "https")
    .WithLiveKitServerReference(livekit, includeCredentials: true)
    .WaitFor(livekit)
    .WithHttpHealthCheck("/health")
    .WithTags("backend");

builder.Build().Run();
