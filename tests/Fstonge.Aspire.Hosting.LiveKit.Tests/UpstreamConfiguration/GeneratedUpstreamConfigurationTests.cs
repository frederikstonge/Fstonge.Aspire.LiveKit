using YamlDotNet.Serialization;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.UpstreamConfiguration;

public sealed class GeneratedUpstreamConfigurationTests
{
    [Fact]
    public void CommonNestedConfigurationProperties_AreInitialized()
    {
        var server = new global::Aspire.Hosting.LiveKit.Configuration.Server.LiveKitServerUpstreamConfiguration();
        var ingress = new global::Aspire.Hosting.LiveKit.Configuration.Ingress.LiveKitIngressUpstreamConfiguration();
        var egress = new global::Aspire.Hosting.LiveKit.Configuration.Egress.LiveKitEgressUpstreamConfiguration();
        var sip = new global::Aspire.Hosting.LiveKit.Configuration.Sip.LiveKitSipUpstreamConfiguration();

        Assert.NotNull(server.Logging);
        Assert.NotNull(server.Rtc);
        Assert.NotNull(server.Redis);
        Assert.NotNull(ingress.Logging);
        Assert.NotNull(ingress.CpuCost);
        Assert.NotNull(ingress.Redis);
        Assert.NotNull(egress.Logging);
        Assert.NotNull(egress.Backup);
        Assert.NotNull(sip.Logging);
        Assert.NotNull(sip.Tls);
    }

    [Fact]
    public void IngressConfiguration_SerializesAndDeserializesUsingUpstreamYamlNames()
    {
        var configuration = new global::Aspire.Hosting.LiveKit.Configuration.Ingress.LiveKitIngressUpstreamConfiguration
        {
            ApiKey = "devkey",
            CpuCost = new global::Aspire.Hosting.LiveKit.Configuration.Ingress.CPUCostConfig { RtmpCpuCost = 2 },
        };
        var serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
        var deserializer = new DeserializerBuilder().Build();

        var yaml = serializer.Serialize(configuration);
        var roundTripped = deserializer.Deserialize<global::Aspire.Hosting.LiveKit.Configuration.Ingress.LiveKitIngressUpstreamConfiguration>(yaml);

        Assert.Contains("api_key: devkey", yaml);
        Assert.Contains("cpu_cost:", yaml);
        Assert.Contains("rtmp_cpu_cost: 2", yaml);
        Assert.DoesNotContain("apiKey:", yaml);
        Assert.Equal("devkey", roundTripped.ApiKey);
        Assert.Equal(2, roundTripped.CpuCost?.RtmpCpuCost);
    }

    [Fact]
    public void ServerConfiguration_SerializesNestedRtcSettingsUsingUpstreamYamlNames()
    {
        var configuration = new global::Aspire.Hosting.LiveKit.Configuration.Server.LiveKitServerUpstreamConfiguration
        {
            LogLevel = "info",
            Rtc = new global::Aspire.Hosting.LiveKit.Configuration.Server.RTCConfig
            {
                UseExternalIp = true,
                PortRangeStart = 50000,
                PortRangeEnd = 60000,
            },
        };
        var serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
        var deserializer = new DeserializerBuilder().Build();

        var yaml = serializer.Serialize(configuration);
        var roundTripped = deserializer.Deserialize<global::Aspire.Hosting.LiveKit.Configuration.Server.LiveKitServerUpstreamConfiguration>(yaml);

        Assert.Contains("log_level: info", yaml);
        Assert.Contains("port_range_start: 50000", yaml);
        Assert.Equal(true, roundTripped.Rtc?.UseExternalIp);
        Assert.Equal(60000, roundTripped.Rtc?.PortRangeEnd);
    }

    [Fact]
    public void EgressConfiguration_SerializesAndDeserializesUsingUpstreamYamlNames()
    {
        var configuration = new global::Aspire.Hosting.LiveKit.Configuration.Egress.LiveKitEgressUpstreamConfiguration
        {
            TemplatePort = 7981,
            PulseSinkReapGraceSec = 45,
            EnableChromeSandbox = false,
        };
        var serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
        var deserializer = new DeserializerBuilder().Build();

        var yaml = serializer.Serialize(configuration);
        var roundTripped = deserializer.Deserialize<global::Aspire.Hosting.LiveKit.Configuration.Egress.LiveKitEgressUpstreamConfiguration>(yaml);

        Assert.Contains("template_port: 7981", yaml);
        Assert.Contains("pulse_sink_reap_grace_sec: 45", yaml);
        Assert.Contains("enable_chrome_sandbox: false", yaml);
        Assert.Equal(7981, roundTripped.TemplatePort);
        Assert.Equal(45, roundTripped.PulseSinkReapGraceSec);
    }

    [Fact]
    public void SipConfiguration_SerializesAndDeserializesUsingUpstreamYamlNames()
    {
        var configuration = new global::Aspire.Hosting.LiveKit.Configuration.Sip.LiveKitSipUpstreamConfiguration
        {
            ClusterId = "sip-cluster",
            MaxActiveCalls = 100,
        };
        var serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
        var deserializer = new DeserializerBuilder().Build();

        var yaml = serializer.Serialize(configuration);
        var roundTripped = deserializer.Deserialize<global::Aspire.Hosting.LiveKit.Configuration.Sip.LiveKitSipUpstreamConfiguration>(yaml);

        Assert.Contains("cluster_id: sip-cluster", yaml);
        Assert.Contains("max_active_calls: 100", yaml);
        Assert.Equal("sip-cluster", roundTripped.ClusterId);
        Assert.Equal(100, roundTripped.MaxActiveCalls);
    }
}
