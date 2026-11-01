using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Net.Sockets;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Sip;

[Collection(DistributedApplicationBuilderTestCollection.Name)]
public sealed class LiveKitSipHostingExtensionsTests(DistributedApplicationBuilderTestHost appHost)
{
    [Fact]
    public async Task AddSip_RegistersSignalingAndRtpListenersAndRuntimeConfiguration()
    {
        var builder = appHost.CreateBuilder();
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "test-secret", secret: true);
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret);
        var redis = builder.AddRedis("redis");
        var sip = livekit.AddSip("sip", redis, version: "v1.9.0", healthPort: 8082)
            .WithSipTls("/certs/sip.crt", "/certs/sip.key", tlsPort: 5061)
            .WithSipConfiguration(configuration =>
            {
                configuration.ClusterId = "sip-cluster";
                configuration.MaxActiveCalls = 100;
            })
            .WithSipAdditionalConfiguration("use_external_ip: true");

        var image = Assert.Single(sip.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var endpoints = sip.Resource.Annotations.OfType<EndpointAnnotation>().ToArray();
        var yaml = await appHost.GetEnvironmentValueAsync(sip.Resource, "SIP_CONFIG_BODY");

        Assert.Equal("livekit/sip", image.Image);
        Assert.Equal("v1.9.0", image.Tag);
        Assert.Contains(endpoints, endpoint => endpoint.Name == "sip-udp" && endpoint.Protocol == ProtocolType.Udp);
        Assert.Contains(endpoints, endpoint => endpoint.Name == "sip-tcp" && endpoint.Protocol == ProtocolType.Tcp);
        Assert.Contains(endpoints, endpoint => endpoint.Name == "sip-tls" && endpoint.Protocol == ProtocolType.Tcp);
        Assert.Contains("sip_port: 5060", yaml);
        Assert.Contains("rtp_port: \"10000-20000\"", yaml);
        Assert.Contains("health_port: 8082", yaml);
        Assert.Contains("cluster_id: sip-cluster", yaml);
        Assert.Contains("max_active_calls: 100", yaml);
        Assert.Contains("tls:", yaml);
        Assert.Contains("cert_file: \"/certs/sip.crt\"", yaml);
        Assert.Contains("use_external_ip: true", yaml);
        Assert.DoesNotContain("test-key", yaml);
        Assert.DoesNotContain("test-secret", yaml);
    }

    [Fact]
    public void AddSip_RejectsInvalidListenerRangesAndManagedOverrides()
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            livekit.AddSip("bad-range", redis, rtpPortStart: 20000, rtpPortEnd: 10000));
        Assert.Throws<ArgumentException>(() =>
            livekit.AddSip("overlap", redis, sipPort: 12000, rtpPortStart: 10000, rtpPortEnd: 20000));
        Assert.Throws<ArgumentException>(() =>
            livekit.AddSip("same-port", redis, healthPort: 8081, prometheusPort: 8081));
        Assert.Throws<ArgumentException>(() =>
            livekit.AddSip("companion-overlap", redis, sipPort: 8081, healthPort: 8081));
    }

    [Fact]
    public void WithSipTls_RejectsConflictingTlsListenerPorts()
    {
        var builder = appHost.CreateBuilder();
        var sip = builder.AddLiveKit("livekit").AddSip("sip", builder.AddRedis("redis"));

        Assert.Throws<ArgumentException>(() => sip.WithSipTls("/cert.crt", "/cert.key", tlsPort: 5060));
        Assert.Throws<ArgumentException>(() => sip.WithSipTls("/cert.crt", "/cert.key", tlsPort: 10001));
        sip.WithSipTls("/cert.crt", "/cert.key", tlsPort: 5061);
        Assert.Throws<InvalidOperationException>(() => sip.WithSipTls("/other.crt", "/other.key", tlsPort: 5062));
    }

    [Fact]
    public void WithSipAdditionalConfiguration_RejectsManagedOverrides()
    {
        var builder = appHost.CreateBuilder();
        var sip = builder.AddLiveKit("livekit").AddSip("sip", builder.AddRedis("redis"));

        Assert.Throws<ArgumentException>(() => sip.WithSipAdditionalConfiguration("sip_port: 6060"));
    }
}
