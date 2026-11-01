using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.Logging;

public sealed class LiveKitStructuredLogForwarderTests
{
    [Fact]
    public async Task StartAsync_ReportsMissingOtlpEndpoint()
    {
        var resource = DistributedApplicationBuilderTestHost.CreateBuilder().AddLiveKitServer("livekit").Resource;
        using var forwarder = new LiveKitStructuredLogForwarder(
            resource,
            null!,
            new ConfigurationBuilder().Build(),
            NullLogger<LiveKitStructuredLogForwarder>.Instance);

        await forwarder.StartAsync(CancellationToken.None);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await forwarder.ExecuteTask!);

        Assert.Contains("no valid OTLP endpoint is configured", exception.Message);
        Assert.Contains("livekit", exception.Message);
    }
}
