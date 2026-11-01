using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Net.Sockets;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Ingress;

[Collection(DistributedApplicationBuilderTestCollection.Name)]
public sealed class LiveKitIngressHostingExtensionsTests(DistributedApplicationBuilderTestHost appHost)
{
    [Fact]
    public async Task AddIngress_RegistersResourceEndpointsAndRuntimeConfiguration()
    {
        var builder = appHost.CreateBuilder();
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "test-secret", secret: true);
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret);
        var redis = builder.AddRedis("redis");

        Assert.Empty(builder.Resources.OfType<LiveKitIngressResource>());
        var ingress = livekit.AddIngress(
            "ingress",
            redis,
            "rtmps://example.test/live",
            "https://example.test/whip",
            version: "v1.9.0");
        var image = Assert.Single(ingress.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var endpoints = ingress.Resource.Annotations.OfType<EndpointAnnotation>().ToArray();
        var yaml = await appHost.GetEnvironmentValueAsync(ingress.Resource, "INGRESS_CONFIG_BODY");

        Assert.Single(builder.Resources.OfType<LiveKitIngressResource>());
        Assert.Equal("livekit/ingress", image.Image);
        Assert.Equal("v1.9.0", image.Tag);
        Assert.Contains(endpoints, endpoint => endpoint.Name == "rtmp" && endpoint.Protocol == ProtocolType.Tcp);
        Assert.Contains(endpoints, endpoint => endpoint.Name == "whip" && endpoint.Protocol == ProtocolType.Tcp);
        Assert.Contains(endpoints, endpoint => endpoint.Name == "whip-udp" && endpoint.Protocol == ProtocolType.Udp);
        Assert.Contains("api_key: test-key", yaml);
        Assert.Contains("api_secret: test-secret", yaml);
        Assert.Contains("ws_url: \"ws://livekit:7880\"", yaml);
        Assert.Contains("redis:", yaml);
        Assert.Contains("rtmp_base_url: rtmps://example.test/live", await appHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG"));
    }

    [Fact]
    public async Task WithIngressConfiguration_EmitsTypedAndAdditionalSettings()
    {
        var builder = appHost.CreateBuilder();
        var ingress = builder.AddLiveKit("livekit")
            .AddIngress("ingress", builder.AddRedis("redis"), "rtmp://example.test/live", "http://example.test/whip")
            .WithIngressConfiguration(configuration =>
            {
                configuration.CpuCost.RtmpCpuCost = 2;
                configuration.Logging.Level = "info";
            })
            .WithIngressAdditionalConfiguration("custom_setting: true");

        var yaml = await appHost.GetEnvironmentValueAsync(ingress.Resource, "INGRESS_CONFIG_BODY");

        Assert.Contains("rtmp_cpu_cost: 2", yaml);
        Assert.Contains("level: info", yaml);
        Assert.Contains("custom_setting: true", yaml);
    }

    [Theory]
    [InlineData("https://example.test/whip", "rtmp://example.test/live")]
    [InlineData("rtmp://example.test/live", "ftp://example.test/whip")]
    public void AddIngress_RejectsUnsupportedPublicUrlSchemes(string rtmpUrl, string whipUrl)
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");

        Assert.Throws<ArgumentException>(() => livekit.AddIngress("ingress", redis, rtmpUrl, whipUrl));
    }

    [Fact]
    public void AddIngress_RejectsInvalidOrCollidingPorts()
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            livekit.AddIngress("bad", redis, "rtmp://example.test/live", "http://example.test/whip", rtmpPort: 0));
        Assert.Throws<ArgumentException>(() =>
            livekit.AddIngress("duplicate", redis, "rtmp://example.test/live", "http://example.test/whip", rtmpPort: 8080, whipPort: 8080));
    }

    [Theory]
    [InlineData("invalid: [yaml")]
    [InlineData("custom: true\nCUSTOM: false")]
    [InlineData("api_key: managed")]
    public void WithIngressAdditionalConfiguration_RejectsMalformedDuplicateOrManagedSettings(string yaml)
    {
        var builder = appHost.CreateBuilder();
        var ingress = builder.AddLiveKit("livekit")
            .AddIngress("ingress", builder.AddRedis("redis"), "rtmp://example.test/live", "http://example.test/whip");

        Assert.Throws<ArgumentException>(() => ingress.WithIngressAdditionalConfiguration(yaml));
    }
}
