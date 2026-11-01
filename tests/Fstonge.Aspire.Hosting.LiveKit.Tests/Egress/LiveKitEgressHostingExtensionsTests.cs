using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Egress;

[Collection(DistributedApplicationBuilderTestCollection.Name)]
public sealed class LiveKitEgressHostingExtensionsTests(DistributedApplicationBuilderTestHost appHost)
{
    [Fact]
    public async Task AddEgress_RegistersChildResourceAndGeneratesRuntimeConfiguration()
    {
        var builder = appHost.CreateBuilder();
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "test-secret", secret: true);
        var livekit = builder.AddLiveKit("livekit", apiKey, apiSecret);
        var redis = builder.AddRedis("redis");

        Assert.Empty(builder.Resources.OfType<LiveKitEgressResource>());
        var egress = livekit.AddEgress("egress", redis, version: "v1.9.0", healthPort: 8081, prometheusPort: 9090)
            .WithEgressLocalOutputDirectory("./recordings", "/out");
        var image = Assert.Single(egress.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var mount = Assert.Single(egress.Resource.Annotations.OfType<ContainerMountAnnotation>());
        var yaml = await appHost.GetEnvironmentValueAsync(egress.Resource, "EGRESS_CONFIG_BODY");
        var environment = await appHost.BuildEnvironmentAsync(egress.Resource);

        Assert.Equal("livekit/egress", image.Image);
        Assert.Equal("v1.9.0", image.Tag);
        Assert.Equal("/out", mount.Target);
        Assert.Contains("health_port: 8081", yaml);
        Assert.Contains("prometheus_port: 9090", yaml);
        Assert.Contains("redis:", yaml);
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_API_KEY" && pair.Value == "test-key");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_API_SECRET" && pair.Value == "test-secret");
        Assert.DoesNotContain("test-key", yaml);
        Assert.DoesNotContain("test-secret", yaml);
    }

    [Fact]
    public async Task AddEgress_EmitsTypedAndAdditionalConfiguration()
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis)
            .WithEgressConfiguration(configuration =>
            {
                configuration.TemplatePort = 7981;
                configuration.PulseSinkReapGraceSeconds = 45;
                configuration.EnableChromeSandbox = true;
                configuration.Logging.Level = "debug";
            })
            .WithEgressAdditionalConfiguration("custom_setting:\n  enabled: true");

        var yaml = await appHost.GetEnvironmentValueAsync(egress.Resource, "EGRESS_CONFIG_BODY");

        Assert.Contains("template_port: 7981", yaml);
        Assert.Contains("pulse_sink_reap_grace_sec: 45", yaml);
        Assert.Contains("enable_chrome_sandbox: true", yaml);
        Assert.Contains("level: debug", yaml);
        Assert.Contains("custom_setting:", yaml);
    }

    [Fact]
    public void AddEgress_RejectsDuplicateOrInvalidListenerPorts()
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit");
        var redis = builder.AddRedis("redis");

        Assert.Throws<ArgumentOutOfRangeException>(() => livekit.AddEgress("bad", redis, healthPort: 0));
        var exception = Assert.Throws<ArgumentException>(() =>
            livekit.AddEgress("duplicate", redis, healthPort: 8081, prometheusPort: 8081));
        Assert.Equal("prometheusPort", exception.ParamName);
    }

    [Fact]
    public void WithEgressLocalOutputDirectory_RejectsInvalidAndDuplicateMounts()
    {
        var builder = appHost.CreateBuilder();
        var egress = builder.AddLiveKit("livekit").AddEgress("egress", builder.AddRedis("redis"));

        Assert.Throws<ArgumentException>(() => egress.WithEgressLocalOutputDirectory("", "/out"));
        Assert.Throws<ArgumentException>(() => egress.WithEgressLocalOutputDirectory("./recordings", "out"));
        egress.WithEgressLocalOutputDirectory("./recordings", "/out");
        Assert.Throws<InvalidOperationException>(() => egress.WithEgressLocalOutputDirectory("./other", "/other"));
    }

    [Fact]
    public async Task WithEgressS3Storage_KeepsCredentialsInEnvironment()
    {
        var builder = appHost.CreateBuilder();
        var accessKey = builder.AddParameter("access-key", "access-value", secret: true);
        var secret = builder.AddParameter("secret-key", "secret-value", secret: true);
        var egress = builder.AddLiveKit("livekit")
            .AddEgress("egress", builder.AddRedis("redis"))
            .WithEgressS3Storage("recordings", "us-east-1", accessKey, secret, "https://s3.example.test");

        var yaml = await appHost.GetEnvironmentValueAsync(egress.Resource, "EGRESS_CONFIG_BODY");
        var environment = await appHost.BuildEnvironmentAsync(egress.Resource);

        Assert.Contains("bucket: \"recordings\"", yaml);
        Assert.Contains("endpoint: \"https://s3.example.test\"", yaml);
        Assert.Contains(environment, pair => pair.Key == "AWS_ACCESS_KEY_ID" && pair.Value == "access-value");
        Assert.Contains(environment, pair => pair.Key == "AWS_SECRET_ACCESS_KEY" && pair.Value == "secret-value");
        Assert.DoesNotContain("access-value", yaml);
        Assert.DoesNotContain("secret-value", yaml);
    }

    [Fact]
    public void WithEgressS3Storage_RequiresBothCredentials()
    {
        var builder = appHost.CreateBuilder();
        var accessKey = builder.AddParameter("access-key", "access-value", secret: true);
        var egress = builder.AddLiveKit("livekit")
            .AddEgress("egress", builder.AddRedis("redis"));

        Assert.Throws<ArgumentException>(() =>
            egress.WithEgressS3Storage("recordings", "us-east-1", accessKey: accessKey));
        Assert.Throws<ArgumentException>(() =>
            egress.WithEgressS3Storage("recordings", "us-east-1", secret: accessKey));
    }

    [Fact]
    public async Task WithEgressAzureStorage_UsesSecretEnvironmentVariables()
    {
        var builder = appHost.CreateBuilder();
        var accountName = builder.AddParameter("azure-account", "account-value", secret: true);
        var accountKey = builder.AddParameter("azure-key", "key-value", secret: true);
        var egress = builder.AddLiveKit("livekit")
            .AddEgress("egress", builder.AddRedis("redis"))
            .WithEgressAzureStorage("recordings", accountName, accountKey);

        var yaml = await appHost.GetEnvironmentValueAsync(egress.Resource, "EGRESS_CONFIG_BODY");
        var environment = await appHost.BuildEnvironmentAsync(egress.Resource);

        Assert.Contains("container_name: recordings", yaml);
        Assert.Contains(environment, pair => pair.Key == "AZURE_STORAGE_ACCOUNT" && pair.Value == "account-value");
        Assert.Contains(environment, pair => pair.Key == "AZURE_STORAGE_KEY" && pair.Value == "key-value");
        Assert.DoesNotContain("account-value", yaml);
        Assert.DoesNotContain("key-value", yaml);
        Assert.Throws<InvalidOperationException>(() => egress.WithEgressS3Storage("other", "us-east-1"));
    }

    [Theory]
    [InlineData("invalid: [yaml")]
    [InlineData("custom: true\nCUSTOM: false")]
    [InlineData("Template_Port: 8000")]
    public void WithEgressAdditionalConfiguration_RejectsMalformedDuplicateOrManagedSettings(string yaml)
    {
        var builder = appHost.CreateBuilder();
        var egress = builder.AddLiveKit("livekit").AddEgress("egress", builder.AddRedis("redis"));

        Assert.Throws<ArgumentException>(() => egress.WithEgressAdditionalConfiguration(yaml));
    }
}
