using Aspire.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.LiveKit;

[Collection(DistributedApplicationBuilderTestCollection.Name)]
public sealed class LiveKitRedisConfigurationTests(DistributedApplicationBuilderTestHost appHost)
{
    [Fact]
    public async Task Create_UsesRedisAddressAndOptionalPasswordAndTls()
    {
        var builder = appHost.CreateBuilder();
        var password = builder.AddParameter("redis-password", "redis-secret", secret: true);
        var redis = builder.AddRedis("redis").WithPassword(password);
        redis.Resource.PrimaryEndpoint.EndpointAnnotation.TlsEnabled = true;
        var livekit = builder.AddLiveKit("livekit").WithRedis(redis);

        var configuration = await appHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG");

        Assert.Contains("address: redis:6379", configuration);
        Assert.Contains("password:", configuration);
        Assert.Contains("redis-secret", configuration);
        Assert.Contains("tls:", configuration);
        Assert.Contains("enabled: true", configuration);
    }

    [Fact]
    public void Create_UsesConnectionStringForNonRedisResources()
    {
        var builder = appHost.CreateBuilder();
        var connectionString = builder.AddConnectionString("cache", "redis://cache.example.test:6379");

        var configuration = LiveKitRedisConfiguration.Create(connectionString).ToString();

        Assert.Contains("redis:", configuration);
        Assert.Contains("redis://cache.example.test:6379", configuration);
    }
}
