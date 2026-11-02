using IngressConfig = Aspire.Hosting.LiveKit.Configuration.Ingress;
using EgressConfig = Aspire.Hosting.LiveKit.Configuration.Egress;
using SipConfig = Aspire.Hosting.LiveKit.Configuration.Sip;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
var livekit = builder.AddLiveKitServer("livekit")
    .WithLiveKitServerRedis(cache)
    .WithStructuredLogs()
    .WithLiveKitServerConfiguration(config => 
    {
        config.LogLevel = "info";
    });

livekit.AddIngress(
    "ingress",
    cache,
    rtmpBaseUrl: "rtmp://localhost:1935/live",
    whipBaseUrl: "http://localhost:8080/whip")
    .WithStructuredLogs()
    .WithIngressConfiguration(config =>
    {
        config.Logging = new IngressConfig.Config_7837b7412944 { Level = "info" };
    });

livekit.AddEgress(
    "egress",
    cache,
    healthPort: 8081,
    prometheusPort: 9090)
    .WithStructuredLogs()
    .WithEgressConfiguration(config =>
    {
        config.Logging = new EgressConfig.Config_7837b7412944 { Level = "info" };
    });

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
        config.Logging = new SipConfig.Config_7837b7412944 { Level = "info" };
    });

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
                .WithAgentStructuredLogs();
        }
        else
        {
            livekit.AddPythonAgent(
            "python-voice-agent",
            "../Fstonge.Aspire.LiveKit.Example.PythonAgent",
            scriptPath: "main.py")
            .WithOpenAiApiKey(openAiKey)
            .WithAgentStructuredLogs();
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
                .WithAgentStructuredLogs();
        }
        else
        {
            livekit.AddNodeAgent(
                "node-voice-agent",
                "../Fstonge.Aspire.LiveKit.Example.NodeAgent",
                scriptPath: "agent.js")
                .WithOpenAiApiKey(openAiKey)
                .WithAgentStructuredLogs();
        }
    }
}

var apiService = builder.AddProject<Projects.Fstonge_Aspire_LiveKit_Example_ApiService>("apiservice")
    .WithLiveKitServerReference(livekit, includeCredentials: true)
    .WaitFor(livekit)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
