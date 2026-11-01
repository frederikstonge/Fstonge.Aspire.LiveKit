using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DistributedApplicationBuilderTestCollection
    : ICollectionFixture<DistributedApplicationBuilderTestHost>
{
    public const string Name = "Distributed application builder tests";
}

public sealed class DistributedApplicationBuilderTestHost
{
    public IDistributedApplicationBuilder CreateBuilder() =>
        DistributedApplication.CreateBuilder(Array.Empty<string>());

    public async Task<IReadOnlyList<KeyValuePair<string, string>>> BuildEnvironmentAsync(IResource resource)
    {
        var context = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var result = await ExecutionConfigurationBuilder
            .Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(context, NullLogger.Instance, TestContext.Current.CancellationToken);

        return result.EnvironmentVariables.ToArray();
    }

    public async Task<string> GetEnvironmentValueAsync(IResource resource, string key)
    {
        var variables = await BuildEnvironmentAsync(resource);
        return Assert.Single(variables, pair => pair.Key == key).Value;
    }
}
