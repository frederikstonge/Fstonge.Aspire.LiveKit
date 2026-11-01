using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

public static class DistributedApplicationBuilderTestHost
{
    public static IDistributedApplicationBuilder CreateBuilder() =>
        DistributedApplication.CreateBuilder(Array.Empty<string>());

    public static async Task<IReadOnlyList<KeyValuePair<string, string>>> BuildEnvironmentAsync(IResource resource)
    {
        // Publish mode preserves unresolved Aspire references instead of waiting for runtime resource values.
        var context = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish);
        var result = await ExecutionConfigurationBuilder
            .Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(context, NullLogger.Instance, TestContext.Current.CancellationToken);

        return result.EnvironmentVariables.ToArray();
    }

    public static async Task<string> GetEnvironmentValueAsync(IResource resource, string key)
    {
        var variables = await BuildEnvironmentAsync(resource);
        return Assert.Single(variables, pair => pair.Key == key).Value;
    }
}
