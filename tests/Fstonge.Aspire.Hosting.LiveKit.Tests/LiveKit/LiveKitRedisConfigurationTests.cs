using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.LiveKit;

public sealed class LiveKitRedisConfigurationTests
{
    [Fact]
    public async Task Create_UsesRedisAddressAndOptionalPasswordAndTls()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var password = builder.AddParameter("redis-password", "redis-secret", secret: true);
        var redis = builder.AddRedis("redis").WithPassword(password);
        redis.Resource.PrimaryEndpoint.EndpointAnnotation.TlsEnabled = true;
        var livekit = builder.AddLiveKitServer("livekit").WithLiveKitServerRedis(redis);

        var configuration = await DistributedApplicationBuilderTestHost.GetEnvironmentValueAsync(livekit.Resource, "LIVEKIT_CONFIG");

        Assert.Contains("address: {redis.bindings.tcp.host}:{redis.bindings.tcp.port}", configuration);
        Assert.Contains("password:", configuration);
        Assert.Contains("{redis-password.value}", configuration);
        var redisConfiguration = LiveKitRedisConfiguration.Create(redis);
        Assert.Contains(
            redisConfiguration.ValueProviders,
            provider => provider is ReferenceExpression { IsConditional: true });
    }

    [Fact]
    public void Create_UsesConnectionStringForNonRedisResources()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var connectionString = builder.AddConnectionString("cache", "redis://cache.example.test:6379");

        var configuration = LiveKitRedisConfiguration.Create(connectionString);

        Assert.Equal("redis:\n  address: {0}\n", configuration.Format);
        var connectionStringReference = Assert.IsType<ReferenceExpression>(Assert.Single(configuration.ValueProviders));
        Assert.Equal(
            connectionString.Resource.ConnectionStringExpression.ManifestExpressions,
            connectionStringReference.ManifestExpressions);
    }
}
