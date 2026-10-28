using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Fstonge.Aspire.Hosting.LiveKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
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
    public void AddLiveKit_GeneratesAndPersistsSecretCredentials()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        builder.AddLiveKit("livekit");

        var parameters = builder.Resources
            .OfType<ParameterResource>()
            .ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);
        var apiKey = parameters["livekit-api-key"].Default?.GetDefaultValue();
        var apiSecret = parameters["livekit-api-secret"].Default?.GetDefaultValue();

        Assert.True(parameters["livekit-api-key"].Secret);
        Assert.True(parameters["livekit-api-secret"].Secret);
        Assert.True(apiKey?.Length >= 32);
        Assert.True(apiSecret?.Length >= 32);
        Assert.All(apiKey, character => Assert.True(char.IsAsciiLetterOrDigit(character)));
        Assert.All(apiSecret, character => Assert.True(char.IsAsciiLetterOrDigit(character)));
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
        var redisPassword = builder.AddParameter("redis-password", "redis-secret", secret: true);
        var redis = builder.AddRedis("redis").WithPassword(redisPassword);
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret)
            .WithRedis(redis)
            .WithTurn();
        redis.Resource.PrimaryEndpoint.EndpointAnnotation.TlsEnabled = true;

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
        Assert.Contains("address: redis:6379", configuration);
        Assert.Contains("password: >-", configuration);
        Assert.Contains("redis-secret", configuration);
        Assert.Contains("tls:", configuration);
        Assert.Contains("enabled: true", configuration);
        Assert.DoesNotContain("redis:6379,password=", configuration);
        Assert.DoesNotContain("ssl=true", configuration);
        Assert.Equal(builder.Environment.IsDevelopment(), configuration.Contains("development: true"));

        if (!builder.Environment.IsDevelopment())
        {
            Assert.Contains("at least 32 characters", apiSecret.Resource.Description);
        }
    }

    [Fact]
    public async Task AddLiveKit_EnablesJsonLogsByDefault()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");

        var result = await BuildEnvironmentAsync(livekit.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "LIVEKIT_CONFIG").Value;

        Assert.Contains("logging:\n  json: true\n", configuration);
    }

    [Fact]
    public async Task WithLiveKitServerConfiguration_RejectsInvalidUpdateWithoutChangingExistingConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit")
            .WithLiveKitServerConfiguration(configuration =>
            {
                configuration.LogLevel = "warn";
                configuration.Rtc.UseExternalIp = true;
                configuration.Rtc.PortRangeStart = 50000;
                configuration.Rtc.PortRangeEnd = 60000;
            });

        Assert.Throws<InvalidOperationException>(() =>
            livekit.WithLiveKitServerConfiguration(configuration =>
            {
                configuration.LogLevel = "debug";
                configuration.Rtc.UseExternalIp = false;
                configuration.Rtc.PortRangeStart = 40000;
                configuration.Rtc.PortRangeEnd = null;
            }));

        var result = await BuildEnvironmentAsync(livekit.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "LIVEKIT_CONFIG").Value;

        Assert.Contains("log_level: warn", configuration);
        Assert.Contains("use_external_ip: true", configuration);
        Assert.Contains("port_range_start: 50000", configuration);
        Assert.Contains("port_range_end: 60000", configuration);
        Assert.DoesNotContain("log_level: debug", configuration);
        Assert.DoesNotContain("port_range_start: 40000", configuration);
    }

    [Fact]
    public async Task WithIngressConfiguration_RejectsInvalidUpdateWithoutChangingExistingConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var ingress = livekit.AddIngress(
                "ingress",
                redis,
                "rtmp://localhost:1935/live",
                "http://localhost:8080/whip")
            .WithIngressConfiguration(configuration =>
            {
                configuration.CpuCost.RtmpCpuCost = 2;
                configuration.Logging.Level = "info";
            });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ingress.WithIngressConfiguration(configuration =>
            {
                configuration.CpuCost.RtmpCpuCost = -1;
                configuration.Logging.Level = "debug";
            }));

        var result = await BuildEnvironmentAsync(ingress.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "INGRESS_CONFIG_BODY").Value;

        Assert.Contains("rtmp_cpu_cost: 2", configuration);
        Assert.Contains("level: info", configuration);
        Assert.DoesNotContain("rtmp_cpu_cost: -1", configuration);
        Assert.DoesNotContain("level: debug", configuration);
    }

    [Fact]
    public async Task WithSipConfiguration_RejectsInvalidUpdateWithoutChangingExistingConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var sip = livekit.AddSip("sip", redis)
            .WithSipConfiguration(configuration =>
            {
                configuration.ClusterId = "existing-cluster";
                configuration.Logging.Level = "info";
            });

        Assert.Throws<ArgumentException>(() =>
            sip.WithSipConfiguration(configuration =>
            {
                configuration.ClusterId = "new-cluster";
                configuration.Logging.Level = "trace";
            }));

        var result = await BuildEnvironmentAsync(sip.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "SIP_CONFIG_BODY").Value;

        Assert.Contains("cluster_id: existing-cluster", configuration);
        Assert.Contains("level: info", configuration);
        Assert.DoesNotContain("cluster_id: new-cluster", configuration);
        Assert.DoesNotContain("level: trace", configuration);
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
        Assert.Contains(
            ingress.Resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            relationship => ReferenceEquals(relationship.Resource, livekit.Resource)
                && relationship.Type == "parent");
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
        Assert.Contains(
            egress.Resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            relationship => ReferenceEquals(relationship.Resource, livekit.Resource)
                && relationship.Type == "parent");
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
    public async Task AddEgress_AppliesTypedConfigurationToRuntimeEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis, healthPort: 8081, prometheusPort: 9090)
            .WithEgressConfiguration(configuration =>
            {
                configuration.TemplatePort = 7981;
                configuration.DebugHandlerPort = 8082;
                configuration.PulseSinkReapGraceSeconds = 45;
                configuration.TemplateBase = "https://templates.example.test/";
                configuration.EnableChromeSandbox = true;
                configuration.Logging.Level = "debug";
            });

        var result = await BuildEnvironmentAsync(egress.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Contains("redis:", configuration);
        Assert.Contains("health_port: 8081", configuration);
        Assert.Contains("prometheus_port: 9090", configuration);
        Assert.Contains("template_port: 7981", configuration);
        Assert.Contains("debug_handler_port: 8082", configuration);
        Assert.Contains("pulse_sink_reap_grace_sec: 45", configuration);
        Assert.Contains("template_base: https://templates.example.test/", configuration);
        Assert.Contains("enable_chrome_sandbox: true", configuration);
        Assert.Contains("level: debug", configuration);
        Assert.Contains("json: true", configuration);
    }

    [Fact]
    public async Task AddEgress_RejectsInvalidTypedConfigurationBeforeApplyingIt()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            egress.WithEgressConfiguration(configuration => configuration.TemplatePort = 65536));

        egress.WithEgressConfiguration(configuration => configuration.TemplatePort = 7981);
        var result = await BuildEnvironmentAsync(egress.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Contains("template_port: 7981", configuration);
        Assert.DoesNotContain("template_port: 65536", configuration);
    }

    [Fact]
    public async Task AddEgress_RejectsInvalidUpdateWithoutChangingExistingConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis)
            .WithEgressConfiguration(configuration => configuration.TemplatePort = 7981);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            egress.WithEgressConfiguration(configuration => configuration.TemplatePort = 65536));

        var result = await BuildEnvironmentAsync(egress.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Contains("template_port: 7981", configuration);
        Assert.DoesNotContain("template_port: 65536", configuration);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(65536, null)]
    [InlineData(null, 0)]
    [InlineData(null, -1)]
    [InlineData(null, 65536)]
    public void AddEgress_RejectsNonPositiveListenerPorts(int? healthPort, int? prometheusPort)
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            livekit.AddEgress("egress", redis, healthPort: healthPort, prometheusPort: prometheusPort));
    }

    [Fact]
    public void AddEgress_RejectsDuplicateListenerPorts()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");

        var exception = Assert.Throws<ArgumentException>(() =>
            livekit.AddEgress("egress", redis, healthPort: 8081, prometheusPort: 8081));

        Assert.Equal("prometheusPort", exception.ParamName);
    }

    [Fact]
    public void WithEgressLocalOutputDirectory_ValidatesAndPreventsDuplicateMounts()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis);

        Assert.Throws<ArgumentException>(() => egress.WithEgressLocalOutputDirectory("", "/out"));
        Assert.Throws<ArgumentException>(() => egress.WithEgressLocalOutputDirectory("./recordings", "out"));

        egress.WithEgressLocalOutputDirectory("./recordings", "/out");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            egress.WithEgressLocalOutputDirectory("./other", "/other"));

        Assert.Contains("already been configured", exception.Message);
        Assert.Single(egress.Resource.Annotations.OfType<ContainerMountAnnotation>());
    }

    [Fact]
    public async Task WithEgressAzureStorage_UsesSecretEnvironmentVariablesAndRejectsOtherStorage()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var accountName = builder.AddParameter("azure-account", "account-value", secret: true);
        var accountKey = builder.AddParameter("azure-key", "key-value", secret: true);
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis)
            .WithEgressAzureStorage("recordings", accountName, accountKey);

        var result = await BuildEnvironmentAsync(egress.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Contains("storage:", configuration);
        Assert.Contains("azure:", configuration);
        Assert.Contains("container_name: recordings", configuration);
        Assert.Contains(result, pair => pair.Key == "AZURE_STORAGE_ACCOUNT" && pair.Value == "account-value");
        Assert.Contains(result, pair => pair.Key == "AZURE_STORAGE_KEY" && pair.Value == "key-value");
        Assert.DoesNotContain("account-value", configuration);
        Assert.DoesNotContain("key-value", configuration);
        Assert.Throws<InvalidOperationException>(() =>
            egress.WithEgressS3Storage("other", "us-east-1"));
    }

    [Fact]
    public void WithEgressS3Storage_RequiresBothCredentials()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var accessKey = builder.AddParameter("access-key", "access-value", secret: true);
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis);

        Assert.Throws<ArgumentException>(() =>
            egress.WithEgressS3Storage("recordings", "us-east-1", accessKey: accessKey));
        Assert.Throws<ArgumentException>(() =>
            egress.WithEgressS3Storage("recordings", "us-east-1", secret: accessKey));
    }

    [Fact]
    public async Task WithEgressAdditionalConfiguration_AppendsValidYaml()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis)
            .WithEgressAdditionalConfiguration("custom_setting:\n  enabled: true");

        var result = await BuildEnvironmentAsync(egress.Resource);
        var configuration = Assert.Single(result, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Contains("custom_setting:", configuration);
        Assert.Contains("enabled: true", configuration);
    }

    [Theory]
    [InlineData("invalid: [yaml")]
    [InlineData("custom: true\nCUSTOM: false")]
    [InlineData("Template_Port: 8000")]
    public void WithEgressAdditionalConfiguration_RejectsMalformedDuplicateAndReservedSettings(string yaml)
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis);

        Assert.Throws<ArgumentException>(() => egress.WithEgressAdditionalConfiguration(yaml));
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
        Assert.Contains(
            sip.Resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            relationship => ReferenceEquals(relationship.Resource, livekit.Resource)
                && relationship.Type == "parent");
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
