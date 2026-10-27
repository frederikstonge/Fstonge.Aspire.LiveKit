using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Fstonge.Aspire.Hosting.LiveKit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public sealed class LiveKitHostingExtensionsTests
{
    [Fact]
    public void AddLiveKit_UsesLatestWhenVersionIsOmitted()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");

        var image = Assert.Single(livekit.Resource.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal("livekit/livekit-server", image.Image);
        Assert.Equal("latest", image.Tag);
    }

    [Fact]
    public void AddLiveKit_PreservesExplicitVersionAndExposesRtcEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit", version: "v1.9.0", httpPort: 17880, rtcUdpPort: 17882)
            .WithTurn(udpPort: 13478)
            .WithTurnTls(
                "turn.example.test",
                "/certs/turn.crt",
                "/certs/turn.key",
                tlsPort: 15349);

        var image = Assert.Single(livekit.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var endpoints = livekit.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(endpoint => endpoint.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal("v1.9.0", image.Tag);
        Assert.Contains("http", endpoints);
        Assert.Contains("rtc-tcp", endpoints);
        Assert.Contains("rtc-udp", endpoints);
        Assert.Contains("turn-udp", endpoints);
        Assert.Contains("turn-tls", endpoints);
    }

    [Fact]
    public async Task AddLiveKit_UsesSecretParametersInRuntimeConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "01234567890123456789012345678901", secret: true);
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret).WithTurn();

        var context = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var result = await ExecutionConfigurationBuilder
            .Create(livekit.Resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(context, NullLogger.Instance);
        var configuration = Assert.Single(
            result.EnvironmentVariables,
            pair => pair.Key == "LIVEKIT_CONFIG").Value;

        Assert.Contains("test-key", configuration);
        Assert.Contains("01234567890123456789012345678901", configuration);
        Assert.Contains("udp_port: 3478", configuration);
    }

    [Fact]
    public async Task AddIngress_CreatesSeparateResourceAndSharesRedisConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "01234567890123456789012345678901", secret: true);
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret);

        Assert.Empty(builder.Resources.OfType<LiveKitIngressResource>());

        var redis = builder.AddRedis("redis");
        var ingress = livekit.AddIngress(
            "ingress",
            redis,
            "rtmp://localhost:1935/live",
            "http://localhost:8080/whip",
            "v1.9.0");

        var image = Assert.Single(ingress.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var endpoints = ingress.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(endpoint => endpoint.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal("livekit/ingress", image.Image);
        Assert.Equal("v1.9.0", image.Tag);
        Assert.Contains("rtmp", endpoints);
        Assert.Contains("whip", endpoints);
        Assert.Contains("whip-udp", endpoints);
        Assert.Single(builder.Resources.OfType<LiveKitIngressResource>());

        var context = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var result = await ExecutionConfigurationBuilder
            .Create(ingress.Resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(context, NullLogger.Instance);
        var configuration = Assert.Single(
            result.EnvironmentVariables,
            pair => pair.Key == "INGRESS_CONFIG_BODY").Value;

        Assert.Contains("test", configuration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("redis:", configuration);
        Assert.Contains("ws_url: \"ws://livekit:7880\"", configuration);
    }

    [Theory]
    [InlineData("https://example.test/whip", "rtmp://example.test/live")]
    [InlineData("rtmp://example.test/live", "ftp://example.test/whip")]
    public void AddIngress_RejectsInvalidPublicUrlSchemes(string rtmpUrl, string whipUrl)
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");

        Assert.Throws<ArgumentException>(() =>
            livekit.AddIngress("ingress", redis, rtmpUrl, whipUrl));
    }
}
