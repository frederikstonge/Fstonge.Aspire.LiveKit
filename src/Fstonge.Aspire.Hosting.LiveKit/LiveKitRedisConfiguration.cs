using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

internal static class LiveKitRedisConfiguration
{
    public static ReferenceExpression Create<TResource>(IResourceBuilder<TResource> redis)
        where TResource : IResource, IResourceWithConnectionString
    {
        if (redis.Resource is not RedisResource redisResource)
        {
            return ReferenceExpression.Create($"redis:\n  address: {redis.Resource.ConnectionStringExpression}\n");
        }

        var password = redisResource.PasswordParameter is { } passwordParameter
            ? ReferenceExpression.Create($"  password: >-\n    {passwordParameter}\n")
            : ReferenceExpression.Empty;
        var tls = redisResource.TlsEnabled
            ? ReferenceExpression.Create($"  tls:\n    enabled: true\n")
            : ReferenceExpression.Empty;

        return ReferenceExpression.Create(
            $"redis:\n  address: {redisResource.Host}:{redisResource.Port}\n{password}{tls}");
    }
}
