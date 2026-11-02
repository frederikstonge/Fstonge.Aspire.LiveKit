using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Logging;

public sealed class LiveKitServiceLoggingExtensionsTests
{
    [Fact]
    public void WithJsonLogging_EnablesJsonWithoutRegisteringForwarder()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var ingress = builder.AddLiveKitServer("livekit")
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
        var livekit = builder.AddLiveKitServer("livekit");
        var initialHostedServiceCount = builder.Services.Count(service => service.ServiceType == typeof(IHostedService));

        livekit.WithStructuredLogs().WithStructuredLogs();

        Assert.True(livekit.Resource.JsonLoggingEnabled);
        Assert.True(livekit.Resource.StructuredLogForwardingEnabled);
        Assert.Equal(initialHostedServiceCount + 1, builder.Services.Count(
            service => service.ServiceType == typeof(IHostedService)));

        using var serviceProvider = builder.Services.BuildServiceProvider();
        var registration = builder.Services.Last(service =>
            service.ServiceType == typeof(IHostedService) && service.ImplementationFactory is not null);
        Assert.IsType<LiveKitStructuredLogForwarder>(registration.ImplementationFactory!(serviceProvider));
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

        using var serviceProvider = builder.Services.BuildServiceProvider();
        var registration = builder.Services.Last(service =>
            service.ServiceType == typeof(IHostedService) && service.ImplementationFactory is not null);
        Assert.IsType<LiveKitStructuredLogForwarder>(registration.ImplementationFactory!(serviceProvider));
    }

    [Fact]
    public void ResourceSpecificLoggingWrappers_ConfigureEgressIngressAndAgentResources()
    {
        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var livekit = builder.AddLiveKitServer("livekit");
        var redis = builder.AddRedis("redis");
        var egress = livekit.AddEgress("egress", redis);
        var ingress = livekit.AddIngress("ingress", redis, "rtmp://example.test/live", "http://example.test/whip");
        var agent = builder.AddExecutable("agent", "node", ".");
        var agentBuilder = builder.CreateResourceBuilder<IResource>(agent.Resource);

        Assert.Same(egress, LiveKitEgressHostingExtensions.WithEgressJsonLogging(egress));
        Assert.Same(egress, LiveKitEgressHostingExtensions.WithEgressStructuredLogs(egress));
        Assert.Same(ingress, LiveKitIngressHostingExtensions.WithIngressJsonLogging(ingress));
        Assert.Same(ingress, LiveKitIngressHostingExtensions.WithIngressStructuredLogs(ingress));
        Assert.Same(
            agentBuilder,
            LiveKitAgentHostingExtensions.WithAgentStructuredLogs(
                agentBuilder));

        Assert.True(egress.Resource.StructuredLogForwardingEnabled);
        Assert.True(ingress.Resource.StructuredLogForwardingEnabled);
    }
}
