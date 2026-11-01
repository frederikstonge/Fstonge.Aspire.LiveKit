using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.LiveKit;

[Collection(DistributedApplicationBuilderTestCollection.Name)]
public sealed class LiveKitHostingExtensionsTests(DistributedApplicationBuilderTestHost appHost)
{
    [Fact]
    public void AddLiveKit_UsesLatestImageAndGeneratedSecretParameters()
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit");
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
        var builder = appHost.CreateBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddLiveKit("invalid", httpPort: 0));
        Assert.Throws<ArgumentException>(() => builder.AddLiveKit("duplicate", httpPort: 7881, rtcTcpPort: 7881));
    }

    [Fact]
    public async Task AddLiveKit_EmitsServerConfigurationAndKeepsCredentialsOutOfYamlSettings()
    {
        var builder = appHost.CreateBuilder();
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "01234567890123456789012345678901", secret: true);
        var redisPassword = builder.AddParameter("redis-password", "redis-secret", secret: true);
        var redis = builder.AddRedis("redis").WithPassword(redisPassword);
        redis.Resource.PrimaryEndpoint.EndpointAnnotation.TlsEnabled = true;
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret, version: "v1.9.0")
            .WithRedis(redis)
            .WithTurn(udpPort: 3478)
            .WithTurnTls("turn.example.test", "/certs/turn.crt", "/certs/turn.key", tlsPort: 5349);

        var image = Assert.Single(livekit.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var endpointNames = livekit.Resource.Annotations.OfType<EndpointAnnotation>()
            .Select(endpoint => endpoint.Name)
            .ToHashSet(StringComparer.Ordinal);
        var yaml = await appHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG");

        Assert.Equal("livekit/livekit-server", image.Image);
        Assert.Equal("v1.9.0", image.Tag);
        Assert.Contains("http", endpointNames);
        Assert.Contains("rtc-tcp", endpointNames);
        Assert.Contains("rtc-udp", endpointNames);
        Assert.Contains("turn-udp", endpointNames);
        Assert.Contains("turn-tls", endpointNames);
        Assert.Contains("development: true", yaml);
        Assert.Contains("address: redis:6379", yaml);
        Assert.Contains("password:", yaml);
        Assert.Contains("tls:", yaml);
        Assert.Contains("udp_port: 3478", yaml);
        Assert.Contains("domain: turn.example.test", yaml);
        Assert.Contains("json: true", yaml);
        Assert.Contains("test-key", yaml);
        Assert.Contains("01234567890123456789012345678901", yaml);
    }

    [Fact]
    public async Task WithLiveKitReference_OnlyAddsCredentialsWhenRequested()
    {
        var builder = appHost.CreateBuilder();
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "test-secret", secret: true);
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret);
        var withoutCredentials = builder.AddExecutable("without-credentials", "node", ".")
            .WithLiveKitReference(livekit);
        var withCredentials = builder.AddExecutable("with-credentials", "node", ".")
            .WithLiveKitReference(livekit, includeCredentials: true);

        var without = await appHost.BuildEnvironmentAsync(withoutCredentials.Resource);
        var with = await appHost.BuildEnvironmentAsync(withCredentials.Resource);

        Assert.Contains(without, pair => pair.Key == "LIVEKIT_URL");
        Assert.DoesNotContain(without, pair => pair.Key == "LIVEKIT_API_KEY");
        Assert.DoesNotContain(without, pair => pair.Key == "LIVEKIT_API_SECRET");
        Assert.Contains(with, pair => pair.Key == "LIVEKIT_API_KEY" && pair.Value == "test-key");
        Assert.Contains(with, pair => pair.Key == "LIVEKIT_API_SECRET" && pair.Value == "test-secret");
    }

    [Fact]
    public async Task WithLiveKitServerConfiguration_RetainsExistingSettingsWhenUpdateFails()
    {
        var builder = appHost.CreateBuilder();
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
                configuration.Rtc.PortRangeStart = 40000;
                configuration.Rtc.PortRangeEnd = null;
            }));

        var yaml = await appHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG");
        Assert.Contains("log_level: warn", yaml);
        Assert.Contains("use_external_ip: true", yaml);
        Assert.Contains("port_range_start: 50000", yaml);
        Assert.Contains("port_range_end: 60000", yaml);
        Assert.DoesNotContain("log_level: debug", yaml);
    }

    [Fact]
    public async Task WithLiveKitAdditionalConfiguration_AppendsYamlAndRejectsManagedKeys()
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit")
            .WithLiveKitAdditionalConfiguration("custom_setting:\n  enabled: true");

        var yaml = await appHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG");

        Assert.Contains("custom_setting:", yaml);
        Assert.Contains("enabled: true", yaml);
        Assert.Throws<ArgumentException>(() => livekit.WithLiveKitAdditionalConfiguration("logging:\n  json: false"));
    }

    [Fact]
    public void WithTurnAndTurnTls_ValidatePortsAndReturnConfiguredBuilder()
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit", rtcTcpPort: 7881);

        Assert.Same(livekit, livekit.WithTurn(udpPort: 3478));
        Assert.Same(
            livekit,
            livekit.WithTurnTls("turn.example.test", "/certs/tls.crt", "/certs/tls.key", tlsPort: 5349));
        Assert.Throws<ArgumentOutOfRangeException>(() => livekit.WithTurn(udpPort: 0));
        Assert.Throws<ArgumentException>(() =>
            livekit.WithTurnTls("turn.example.test", "/certs/tls.crt", "/certs/tls.key", tlsPort: 7881));
    }
}
