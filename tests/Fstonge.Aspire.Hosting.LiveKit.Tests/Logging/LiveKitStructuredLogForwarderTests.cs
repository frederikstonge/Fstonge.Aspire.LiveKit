using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Logging;

[Collection(DistributedApplicationBuilderTestCollection.Name)]
public sealed class LiveKitStructuredLogForwarderTests(DistributedApplicationBuilderTestHost appHost)
{
    [Fact]
    public async Task StartAsync_ReportsMissingOtlpEndpoint()
    {
        var resource = appHost.CreateBuilder().AddLiveKit("livekit").Resource;
        using var forwarder = new LiveKitStructuredLogForwarder(
            resource,
            null!,
            new ConfigurationBuilder().Build(),
            NullLogger<LiveKitStructuredLogForwarder>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => forwarder.StartAsync(CancellationToken.None));

        Assert.Contains("no valid OTLP endpoint is configured", exception.Message);
        Assert.Contains("livekit", exception.Message);
    }
}
