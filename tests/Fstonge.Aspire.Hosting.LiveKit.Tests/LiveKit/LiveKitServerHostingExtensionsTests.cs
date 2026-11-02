using Aspire.Hosting;
using Aspire.Hosting.LiveKit.Configuration.Server;
using YamlDotNet.RepresentationModel;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.LiveKit;

public sealed class LiveKitServerHostingExtensionsTests
{
    [Fact]
    public void AddLiveKit_UsesLatestImageAndGeneratedSecretParameters()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKitServer("livekit");
        var image = Assert.Single(livekit.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var parameters = builder.Resources.OfType<ParameterResource>().ToDictionary(p => p.Name);
        var key = parameters["livekit-api-key"];
        var secret = parameters["livekit-api-secret"];

        Assert.Equal("livekit/livekit-server", image.Image);
        Assert.Equal("latest", image.Tag);
        Assert.Null(livekit.Resource.Entrypoint);
        Assert.True(key.Secret);
        Assert.True(secret.Secret);
        Assert.True((key.Default?.GetDefaultValue()?.Length ?? 0) >= 32);
        Assert.True((secret.Default?.GetDefaultValue()?.Length ?? 0) >= 64);
        Assert.All(key.Default!.GetDefaultValue()!, character => Assert.True(char.IsAsciiLetterOrDigit(character)));
        Assert.All(secret.Default!.GetDefaultValue()!, character => Assert.True(char.IsAsciiLetterOrDigit(character)));
    }

    [Fact]
    public void AddLiveKit_RejectsInvalidPortsAndDuplicateHttpTcpPort()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddLiveKitServer("invalid", httpPort: 0));
        Assert.Throws<ArgumentException>(() => builder.AddLiveKitServer("duplicate", httpPort: 7881, rtcTcpPort: 7881));
    }

    [Fact]
    public async Task AddLiveKit_EmitsServerConfigurationAndKeepsCredentialsOutOfYamlSettings()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "01234567890123456789012345678901", secret: true);
        var redisPassword = builder.AddParameter("redis-password", "redis-secret", secret: true);
        var redis = builder.AddRedis("redis").WithPassword(redisPassword);
        redis.Resource.PrimaryEndpoint.EndpointAnnotation.TlsEnabled = true;
        var livekit = builder.AddLiveKitServer("livekit", apiKey, apiSecret, version: "v1.9.0")
            .WithLiveKitServerRedis(redis)
            .WithLiveKitServerTurn(udpPort: 3478)
            .WithLiveKitServerTurnTls("turn.example.test", "/certs/turn.crt", "/certs/turn.key", tlsPort: 5349);

        var image = Assert.Single(livekit.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var endpointNames = livekit.Resource.Annotations.OfType<EndpointAnnotation>()
            .Select(endpoint => endpoint.Name)
            .ToHashSet(StringComparer.Ordinal);
        var yaml = await DistributedApplicationBuilderTestHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG");

        Assert.Equal("livekit/livekit-server", image.Image);
        Assert.Equal("v1.9.0", image.Tag);
        Assert.Contains("http", endpointNames);
        Assert.Contains("rtc-tcp", endpointNames);
        Assert.Contains("rtc-udp", endpointNames);
        Assert.Contains("turn-udp", endpointNames);
        Assert.Contains("turn-tls", endpointNames);
        Assert.Equal(builder.Environment.IsDevelopment(), yaml.Contains("development: true", StringComparison.Ordinal));
        Assert.Contains("address: {redis.bindings.tcp.host}:{redis.bindings.tcp.port}", yaml);
        Assert.Contains("password:", yaml);
        Assert.Contains("\nturn:\n", yaml);
        Assert.Contains("udp_port: 3478", yaml);
        Assert.Contains("domain: turn.example.test", yaml);
        Assert.Contains("json: true", yaml);
        Assert.Contains("{api-key.value}", yaml);
        Assert.Contains("{api-secret.value}", yaml);
    }

    [Fact]
    public async Task AddLiveKit_EmitsDevelopmentConfigurationInDevelopmentEnvironment()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        var livekit = builder.AddLiveKitServer("livekit");

        var yaml = await DistributedApplicationBuilderTestHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG");

        Assert.Contains("development: true", yaml);
    }

    [Fact]
    public async Task WithLiveKitReference_OnlyAddsCredentialsWhenRequested()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "test-secret", secret: true);
        var livekit = builder.AddLiveKitServer("livekit", apiKey, apiSecret);
        var withoutCredentials = builder.AddExecutable("without-credentials", "node", ".")
            .WithLiveKitServerReference(livekit);
        var withCredentials = builder.AddExecutable("with-credentials", "node", ".")
            .WithLiveKitServerReference(livekit, includeCredentials: true);

        var without = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(withoutCredentials.Resource);
        var with = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(withCredentials.Resource);

        Assert.Contains(without, pair => pair.Key == "LIVEKIT_URL");
        Assert.DoesNotContain(without, pair => pair.Key == "LIVEKIT_API_KEY");
        Assert.DoesNotContain(without, pair => pair.Key == "LIVEKIT_API_SECRET");
        Assert.Contains(with, pair => pair.Key == "LIVEKIT_API_KEY" && pair.Value == "{api-key.value}");
        Assert.Contains(with, pair => pair.Key == "LIVEKIT_API_SECRET" && pair.Value == "{api-secret.value}");
    }

    [Fact]
    public async Task AddExternalLiveKit_UsesUrlParameterAndOptionalCredentialsWithoutAddingAContainer()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var url = builder.AddParameter("external-livekit-url", "https://livekit.example.test");
        var externalWithoutCredentials = builder.AddExternalLiveKitServer("external-without-credentials", url);
        var uncredentialedProject = builder.AddExecutable("uncredentialed-api", "node", ".")
            .WithLiveKitServerReference(externalWithoutCredentials);
        var apiKey = builder.AddParameter("external-api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("external-api-secret", "test-secret", secret: true);
        var externalLiveKit = builder.AddExternalLiveKitServer("external-livekit", url, apiKey, apiSecret);
        var project = builder.AddExecutable("api", "node", ".")
            .WithLiveKitServerReference(externalLiveKit, includeCredentials: true);

        var environment = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(project.Resource);
        var uncredentialedEnvironment =
            await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(uncredentialedProject.Resource);

        Assert.DoesNotContain(externalLiveKit.Resource.Annotations, annotation => annotation is ContainerImageAnnotation);
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_URL" && pair.Value == "{external-livekit-url.value}");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_API_KEY" && pair.Value == "{external-api-key.value}");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_API_SECRET" && pair.Value == "{external-api-secret.value}");
        Assert.Contains(uncredentialedEnvironment, pair => pair.Key == "LIVEKIT_URL");
        Assert.DoesNotContain(uncredentialedEnvironment, pair => pair.Key == "LIVEKIT_API_KEY");
        Assert.DoesNotContain(uncredentialedEnvironment, pair => pair.Key == "LIVEKIT_API_SECRET");
        Assert.Throws<InvalidOperationException>(() =>
            builder.AddExecutable("api-without-credentials", "node", ".")
                .WithLiveKitServerReference(externalWithoutCredentials, includeCredentials: true));
    }

    [Fact]
    public void AddExternalLiveKit_RejectsIncompleteCredentials()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var url = builder.AddParameter("external-livekit-url", "https://livekit.example.test");
        var apiKey = builder.AddParameter("external-api-key", "test-key", secret: true);

        Assert.Throws<ArgumentException>(() =>
            builder.AddExternalLiveKitServer("incomplete-credentials", url, apiKey));
    }

    [Fact]
    public async Task WithLiveKitServerConfiguration_RetainsExistingSettingsWhenUpdateFails()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKitServer("livekit")
            .WithLiveKitServerConfiguration(configuration =>
            {
                configuration.LogLevel = "warn";
                configuration.Rtc = new RTCConfig
                {
                    UseExternalIp = true,
                    PortRangeStart = 50000,
                    PortRangeEnd = 60000,
                };
            });

        Assert.Throws<InvalidOperationException>(() =>
            livekit.WithLiveKitServerConfiguration(configuration =>
            {
                configuration.LogLevel = "debug";
                configuration.Rtc = new RTCConfig { PortRangeStart = 40000 };
            }));

        var yaml = await DistributedApplicationBuilderTestHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG");
        Assert.Contains("log_level: warn", yaml);
        Assert.Contains("use_external_ip: true", yaml);
        Assert.Contains("port_range_start: 50000", yaml);
        Assert.Contains("port_range_end: 60000", yaml);
        Assert.DoesNotContain("log_level: debug", yaml);
    }

    [Fact]
    public async Task WithLiveKitServerConfiguration_ProducesOneValidYamlDocument()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKitServer("livekit")
            .WithLiveKitServerConfiguration(configuration =>
            {
                configuration.LogLevel = "info";
                configuration.Rtc = new RTCConfig { UseExternalIp = true };
                configuration.Logging = new LoggingConfig { Json = true };
            });

        var yaml = await DistributedApplicationBuilderTestHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG");
        var yamlStream = new YamlStream();

        yamlStream.Load(new StringReader(yaml));

        var document = Assert.Single(yamlStream.Documents);
        var root = Assert.IsType<YamlMappingNode>(document.RootNode);
        Assert.Contains(new YamlScalarNode("rtc"), root.Children.Keys);
        Assert.Contains(new YamlScalarNode("logging"), root.Children.Keys);
        Assert.Contains(new YamlScalarNode("log_level"), root.Children.Keys);
    }

    [Fact]
    public void WithLiveKitServerConfiguration_RejectsEmptyConfiguration()
    {
        var livekit = DistributedApplicationBuilderTestHost.CreateBuilder().AddLiveKitServer("livekit");

        Assert.Throws<InvalidOperationException>(() =>
            livekit.WithLiveKitServerConfiguration(_ => { }));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 65536)]
    [InlineData(101, 100)]
    public void WithLiveKitServerConfiguration_RejectsInvalidRtcPortRange(int start, int end)
    {
        var livekit = DistributedApplicationBuilderTestHost.CreateBuilder().AddLiveKitServer("livekit");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            livekit.WithLiveKitServerConfiguration(configuration =>
                configuration.Rtc = new RTCConfig
                {
                    PortRangeStart = start,
                    PortRangeEnd = end,
                }));
    }

    [Fact]
    public void WithLiveKitServerConfiguration_RejectsResourceManagedRtcPorts()
    {
        var livekit = DistributedApplicationBuilderTestHost.CreateBuilder().AddLiveKitServer("livekit");

        var exception = Assert.Throws<ArgumentException>(() =>
            livekit.WithLiveKitServerConfiguration(configuration =>
                configuration.Rtc = new RTCConfig { TcpPort = 9000 }));

        Assert.Contains("managed by the LiveKit resource configuration", exception.Message);
    }

    [Fact]
    public async Task WithLiveKitAdditionalConfiguration_AppendsYamlAndRejectsManagedKeys()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKitServer("livekit")
            .WithLiveKitServerAdditionalConfiguration("custom_setting:\n  enabled: true");

        var yaml = await DistributedApplicationBuilderTestHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG");

        Assert.Contains("custom_setting:", yaml);
        Assert.Contains("enabled: true", yaml);
        Assert.Throws<ArgumentException>(() => livekit.WithLiveKitServerAdditionalConfiguration("logging:\n  json: false"));
    }

    [Fact]
    public void WithTurnAndTurnTls_ValidatePortsAndReturnConfiguredBuilder()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKitServer("livekit", rtcTcpPort: 7881);

        Assert.Same(livekit, livekit.WithLiveKitServerTurn(udpPort: 3478));
        Assert.Same(
            livekit,
            livekit.WithLiveKitServerTurnTls("turn.example.test", "/certs/tls.crt", "/certs/tls.key", tlsPort: 5349));
        Assert.Throws<ArgumentOutOfRangeException>(() => livekit.WithLiveKitServerTurn(udpPort: 0));
        Assert.Throws<ArgumentException>(() =>
            livekit.WithLiveKitServerTurnTls("turn.example.test", "/certs/tls.crt", "/certs/tls.key", tlsPort: 7881));
        Assert.Throws<ArgumentException>(() =>
            livekit.WithLiveKitServerTurn(udpPort: livekit.Resource.RtcUdpPort));
    }

    [Fact]
    public void LiveKitServerLoggingWrappers_ConfigureJsonAndStructuredLogging()
    {
        var livekit = DistributedApplicationBuilderTestHost.CreateBuilder().AddLiveKitServer("livekit");

        Assert.Same(livekit, LiveKitServerHostingExtensions.WithLiveKitServerJsonLogging(livekit));
        Assert.True(livekit.Resource.JsonLoggingEnabled);
        Assert.Same(livekit, LiveKitServerHostingExtensions.WithLiveKitServerStructuredLogs(livekit));
        Assert.True(livekit.Resource.StructuredLogForwardingEnabled);
    }
}
