using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Logging;

public sealed class LiveKitServiceLoggingExtensionsTests
{
    [Fact]
    public void WithJsonLogging_EnablesJsonWithoutRegisteringForwarder()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var ingress = builder.AddLiveKit("livekit")
            .AddIngress("ingress", builder.AddRedis("redis"), "rtmp://example.test/live", "http://example.test/whip");
        var hostedServiceCount = builder.Services.Count(service => service.ServiceType == typeof(IHostedService));

        var result = ingress.WithJsonLogging();

        Assert.Same(ingress, result);
        Assert.True(ingress.Resource.JsonLoggingEnabled);
        Assert.False(ingress.Resource.StructuredLogForwardingEnabled);
        Assert.Equal(hostedServiceCount, builder.Services.Count(service => service.ServiceType == typeof(IHostedService)));
    }

    [Fact]
    public void WithStructuredLogs_EnablesJsonAndRegistersOnlyOneForwarderPerResource()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit");
        var initialHostedServiceCount = builder.Services.Count(service => service.ServiceType == typeof(IHostedService));

        livekit.WithStructuredLogs().WithStructuredLogs();

        Assert.True(livekit.Resource.JsonLoggingEnabled);
        Assert.True(livekit.Resource.StructuredLogForwardingEnabled);
        Assert.Equal(initialHostedServiceCount + 1, builder.Services.Count(
            service => service.ServiceType == typeof(IHostedService)));
    }

    [Fact]
    public void WithAgentStructuredLogs_RegistersForwarderForAgentResource()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var agent = builder.AddExecutable("agent", "node", ".");
        var hostedServicesBefore = builder.Services.Count(service => service.ServiceType == typeof(IHostedService));

        agent.WithAgentStructuredLogs();

        Assert.Equal(hostedServicesBefore + 1, builder.Services.Count(
            service => service.ServiceType == typeof(IHostedService)));
    }
}
