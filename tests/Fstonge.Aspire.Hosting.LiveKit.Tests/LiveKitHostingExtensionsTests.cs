using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Fstonge.Aspire.Hosting.LiveKit;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Sockets;

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
        Assert.Null(livekit.Resource.Entrypoint);
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
        Assert.Null(ingress.Resource.Entrypoint);
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

    [Fact]
    public async Task AddEgress_IsOptInAndUsesSharedServerAndRedisConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "01234567890123456789012345678901", secret: true);
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret);
        var redis = builder.AddRedis("redis");

        Assert.Empty(builder.Resources.OfType<LiveKitEgressResource>());

        var egress = livekit.AddEgress("egress", redis, healthPort: 8081, prometheusPort: 9090)
            .WithEgressLocalOutputDirectory("./recordings", "/out");

        var image = Assert.Single(egress.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var endpoints = egress.Resource.Annotations.OfType<EndpointAnnotation>()
            .Select(endpoint => endpoint.Name)
            .ToHashSet(StringComparer.Ordinal);
        var mount = Assert.Single(egress.Resource.Annotations.OfType<ContainerMountAnnotation>());

        Assert.Equal("livekit/egress", image.Image);
        Assert.Equal("latest", image.Tag);
        Assert.Null(egress.Resource.Entrypoint);
        Assert.Contains("health", endpoints);
        Assert.Contains("prometheus", endpoints);
        Assert.Equal("/out", mount.Target);

        var result = await BuildEnvironmentAsync(egress.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Contains("redis:", configuration);
        Assert.Contains("address: redis:", configuration);
        Assert.Contains("health_port: 8081", configuration);
        Assert.Contains("prometheus_port: 9090", configuration);
        Assert.DoesNotContain("test-key", configuration);
        Assert.DoesNotContain("01234567890123456789012345678901", configuration);
        Assert.Contains(result, pair => pair.Key == "LIVEKIT_API_KEY" && pair.Value == "test-key");
        Assert.Contains(result, pair => pair.Key == "LIVEKIT_API_SECRET"
            && pair.Value == "01234567890123456789012345678901");
    }

    [Fact]
    public async Task AddEgress_SupportsSecretAwareStorageAndRejectsManagedOverrides()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "01234567890123456789012345678901", secret: true);
        var accessKey = builder.AddParameter("access-key", "access-value", secret: true);
        var storageSecret = builder.AddParameter("storage-secret", "storage-value", secret: true);
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret);
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis, version: "v1.9.0")
            .WithEgressS3Storage(
                "recordings",
                "us-east-1",
                accessKey,
                storageSecret,
                "https://s3.example.test");

        var result = await BuildEnvironmentAsync(egress.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Contains("storage:", configuration);
        Assert.Contains("bucket: \"recordings\"", configuration);
        Assert.Contains("endpoint: \"https://s3.example.test\"", configuration);
        Assert.Contains(result, pair => pair.Key == "AWS_ACCESS_KEY_ID" && pair.Value == "access-value");
        Assert.Contains(result, pair => pair.Key == "AWS_SECRET_ACCESS_KEY" && pair.Value == "storage-value");
        Assert.DoesNotContain("access-value", configuration);
        Assert.DoesNotContain("storage-value", configuration);
        Assert.Throws<ArgumentException>(() =>
            egress.WithEgressAdditionalConfiguration("redis:\n  address: elsewhere"));
    }

    [Fact]
    public async Task AddSip_ExposesVerifiedSignalingProtocolsAndConfiguresRtpRange()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "01234567890123456789012345678901", secret: true);
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret);
        var redis = builder.AddRedis("redis");
        var sip = livekit.AddSip("sip", redis, version: "v1.9.0", healthPort: 8082)
            .WithSipTls("/certs/sip.crt", "/certs/sip.key")
            .WithSipAdditionalConfiguration("use_external_ip: true");

        var image = Assert.Single(sip.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var endpoints = sip.Resource.Annotations.OfType<EndpointAnnotation>().ToArray();
        var result = await BuildEnvironmentAsync(sip.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "SIP_CONFIG_BODY").Value;

        Assert.Equal("livekit/sip", image.Image);
        Assert.Equal("v1.9.0", image.Tag);
        Assert.Null(sip.Resource.Entrypoint);
        Assert.Contains(endpoints, endpoint => endpoint.Name == "sip-udp" && endpoint.Protocol == ProtocolType.Udp);
        Assert.Contains(endpoints, endpoint => endpoint.Name == "sip-tcp" && endpoint.Protocol == ProtocolType.Tcp);
        Assert.Contains(endpoints, endpoint => endpoint.Name == "sip-tls" && endpoint.Protocol == ProtocolType.Tcp);
        Assert.Contains("sip_port: 5060", configuration);
        Assert.Contains("rtp_port: \"10000-20000\"", configuration);
        Assert.Contains("health_port: 8082", configuration);
        Assert.Contains("tls:", configuration);
        Assert.Contains("cert_file: \"/certs/sip.crt\"", configuration);
        Assert.Contains("use_external_ip: true", configuration);
        Assert.DoesNotContain("test-key", configuration);
        Assert.DoesNotContain("01234567890123456789012345678901", configuration);
    }

    [Fact]
    public void AddSip_RejectsInvalidListenerRangesAndManagedOverrides()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            livekit.AddSip("bad-range", redis, rtpPortStart: 20000, rtpPortEnd: 10000));
        Assert.Throws<ArgumentException>(() =>
            livekit.AddSip("overlap", redis, sipPort: 12000, rtpPortStart: 10000, rtpPortEnd: 20000));
        var sip = livekit.AddSip("sip", redis);
        Assert.Throws<ArgumentException>(() => sip.WithSipAdditionalConfiguration("sip_port: 6060"));
    }

    private static async Task<IEnumerable<KeyValuePair<string, string>>> BuildEnvironmentAsync(IResource resource)
    {
        var context = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var result = await ExecutionConfigurationBuilder
            .Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(context, NullLogger.Instance);
        return result.EnvironmentVariables;
    }
}
