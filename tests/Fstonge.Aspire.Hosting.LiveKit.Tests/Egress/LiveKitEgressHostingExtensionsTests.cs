using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Egress;

public sealed class LiveKitEgressHostingExtensionsTests
{
    [Fact]
    public async Task AddEgress_RegistersChildResourceAndGeneratesRuntimeConfiguration()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var apiKey = builder.AddParameter("api-key", "test-key", secret: true);
        var apiSecret = builder.AddParameter("api-secret", "test-secret", secret: true);
        var livekit = builder.AddLiveKitServer("livekit", apiKey, apiSecret);
        var redis = builder.AddRedis("redis");

        Assert.Empty(builder.Resources.OfType<LiveKitEgressResource>());
        var egress = livekit.AddEgress("egress", redis, version: "v1.9.0", healthPort: 8081, prometheusPort: 9090)
            .WithEgressLocalOutputDirectory("./recordings", "/out");
        var image = Assert.Single(egress.Resource.Annotations.OfType<ContainerImageAnnotation>());
        var mount = Assert.Single(egress.Resource.Annotations.OfType<ContainerMountAnnotation>());
        var environment = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(egress.Resource);
        var yaml = Assert.Single(environment, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Equal("livekit/egress", image.Image);
        Assert.Equal("v1.9.0", image.Tag);
        Assert.Equal("/out", mount.Target);
        Assert.Contains("health_port: 8081", yaml);
        Assert.Contains("prometheus_port: 9090", yaml);
        Assert.Contains("redis:", yaml);
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_API_KEY" && pair.Value == "{api-key.value}");
        Assert.Contains(environment, pair => pair.Key == "LIVEKIT_API_SECRET" && pair.Value == "{api-secret.value}");
        Assert.DoesNotContain("test-key", yaml);
        Assert.DoesNotContain("test-secret", yaml);
    }

    [Fact]
    public async Task AddEgress_EmitsTypedAndAdditionalConfiguration()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKitServer("livekit");
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

        var environment = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(egress.Resource);
        var yaml = Assert.Single(environment, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Contains("template_port: 7981", yaml);
        Assert.Contains("pulse_sink_reap_grace_sec: 45", yaml);
        Assert.Contains("enable_chrome_sandbox: true", yaml);
        Assert.Contains("level: debug", yaml);
        Assert.Contains("custom_setting:", yaml);
    }

    [Fact]
    public void AddEgress_RejectsDuplicateOrInvalidListenerPorts()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKitServer("livekit");
        var redis = builder.AddRedis("redis");

        Assert.Throws<ArgumentOutOfRangeException>(() => livekit.AddEgress("bad", redis, healthPort: 0));
        var exception = Assert.Throws<ArgumentException>(() =>
            livekit.AddEgress("duplicate", redis, healthPort: 8081, prometheusPort: 8081));
        Assert.Equal("prometheusPort", exception.ParamName);
    }

    [Fact]
    public void WithEgressLocalOutputDirectory_RejectsInvalidAndDuplicateMounts()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var egress = builder.AddLiveKitServer("livekit").AddEgress("egress", builder.AddRedis("redis"));

        Assert.Throws<ArgumentException>(() => egress.WithEgressLocalOutputDirectory("", "/out"));
        Assert.Throws<ArgumentException>(() => egress.WithEgressLocalOutputDirectory("./recordings", "out"));
        egress.WithEgressLocalOutputDirectory("./recordings", "/out");
        Assert.Throws<InvalidOperationException>(() => egress.WithEgressLocalOutputDirectory("./other", "/other"));
    }

    [Fact]
    public async Task WithEgressS3Storage_KeepsCredentialsInEnvironment()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var accessKey = builder.AddParameter("access-key", "access-value", secret: true);
        var secret = builder.AddParameter("secret-key", "secret-value", secret: true);
        var egress = builder.AddLiveKitServer("livekit")
            .AddEgress("egress", builder.AddRedis("redis"))
            .WithEgressS3Storage("recordings", "us-east-1", accessKey, secret, "https://s3.example.test");

        var environment = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(egress.Resource);
        var yaml = Assert.Single(environment, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Contains("bucket: recordings", yaml);
        Assert.Contains("endpoint: https://s3.example.test", yaml);
        Assert.Contains(environment, pair => pair.Key == "AWS_ACCESS_KEY_ID" && pair.Value == "{access-key.value}");
        Assert.Contains(environment, pair => pair.Key == "AWS_SECRET_ACCESS_KEY" && pair.Value == "{secret-key.value}");
        Assert.DoesNotContain("access-value", yaml);
        Assert.DoesNotContain("secret-value", yaml);
    }

    [Fact]
    public void WithEgressS3Storage_RequiresBothCredentials()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var accessKey = builder.AddParameter("access-key", "access-value", secret: true);
        var egress = builder.AddLiveKitServer("livekit")
            .AddEgress("egress", builder.AddRedis("redis"));

        Assert.Throws<ArgumentException>(() =>
            egress.WithEgressS3Storage("recordings", "us-east-1", accessKey: accessKey));
        Assert.Throws<ArgumentException>(() =>
            egress.WithEgressS3Storage("recordings", "us-east-1", secret: accessKey));
    }

    [Fact]
    public async Task WithEgressAzureStorage_UsesSecretEnvironmentVariables()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var accountName = builder.AddParameter("azure-account", "account-value", secret: true);
        var accountKey = builder.AddParameter("azure-key", "key-value", secret: true);
        var egress = builder.AddLiveKitServer("livekit")
            .AddEgress("egress", builder.AddRedis("redis"))
            .WithEgressAzureStorage("recordings", accountName, accountKey);

        var environment = await DistributedApplicationBuilderTestHost.BuildEnvironmentAsync(egress.Resource);
        var yaml = Assert.Single(environment, pair => pair.Key == "EGRESS_CONFIG_BODY").Value;

        Assert.Contains("container_name: recordings", yaml);
        Assert.Contains(environment, pair => pair.Key == "AZURE_STORAGE_ACCOUNT" && pair.Value == "{azure-account.value}");
        Assert.Contains(environment, pair => pair.Key == "AZURE_STORAGE_KEY" && pair.Value == "{azure-key.value}");
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
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var egress = builder.AddLiveKitServer("livekit").AddEgress("egress", builder.AddRedis("redis"));

        Assert.Throws<ArgumentException>(() => egress.WithEgressAdditionalConfiguration(yaml));
    }
}
