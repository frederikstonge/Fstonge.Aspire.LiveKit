using Fstonge.Aspire.Hosting.LiveKit;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
var livekit = builder.AddLiveKit("livekit")
    .WithRedis(cache)
    .WithStructuredLogs()
    .WithLiveKitServerConfiguration(config => 
    {
        config.LogLevel = "Info";
    });

livekit.AddIngress(
    "ingress",
    cache,
    rtmpBaseUrl: "rtmp://localhost:1935/live",
    whipBaseUrl: "http://localhost:8080/whip")
    .WithStructuredLogs()
    .WithIngressConfiguration(config =>
    {
        config.Logging ??= new()
        {
            Level = "Info",
        };
    });

livekit.AddEgress(
    "egress",
    cache,
    healthPort: 8081,
    prometheusPort: 9090)
    .WithStructuredLogs()
    .WithEgressConfiguration(config =>
    {
        config.Logging ??= new()
        {
            Level = "Info",
        };
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
        config.Logging ??= new()
        {
            Level = "Info",
        };
    });

var apiService = builder.AddProject<Projects.Fstonge_Aspire_LiveKit_Example_ApiService>("apiservice")
    .WithLiveKitReference(livekit, includeCredentials: true)
    .WaitFor(livekit)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
