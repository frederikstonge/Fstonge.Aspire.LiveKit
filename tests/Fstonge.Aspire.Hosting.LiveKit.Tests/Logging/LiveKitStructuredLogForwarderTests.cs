using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;

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

        await forwarder.StartAsync(TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await forwarder.ExecuteTask!);

        Assert.Contains("no valid OTLP endpoint is configured", exception.Message);
        Assert.Contains("livekit", exception.Message);
    }

    [Fact]
    public async Task StartAsync_ValidatesOtlpConfigurationBeforeWatchingLogs()
    {
        await AssertFailsAfterSetupAsync(
            "http://localhost:4318",
            ("OTEL_EXPORTER_OTLP_LOGS_HEADERS", "logs-key=logs-value"),
            ("OTEL_EXPORTER_OTLP_LOGS_PROTOCOL", "grpc"));
        await AssertFailsAfterSetupAsync(
            "http://localhost:4318",
            ("OTEL_EXPORTER_OTLP_HEADERS", "global-key=global-value"),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"));
        await AssertFailsAfterSetupAsync(
            "http://localhost:4318/v1/logs",
            ("AppHost:OtlpApiKey", "apphost-key"));
        await AssertFailsAfterSetupAsync("http://localhost:4318");
        await AssertFailsAfterSetupAsync(
            "http://localhost:4318",
            new InvalidOperationException(),
            ("OTEL_EXPORTER_OTLP_LOGS_PROTOCOL", "unsupported"));
    }

    [Fact]
    public async Task StartAsync_ForwardsResourceLogsToOtlp()
    {
        using var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var builder = DistributedApplicationBuilderTestHost.CreateBuilder();
        var resource = builder.AddLiveKitServer("livekit").Resource;
        using var serviceProvider = builder.Services.BuildServiceProvider();
        var resourceLoggerService = serviceProvider.GetRequiredService<ResourceLoggerService>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"] = $"http://127.0.0.1:{port}/v1/logs",
                ["OTEL_EXPORTER_OTLP_LOGS_PROTOCOL"] = "http/protobuf"
            })
            .Build();
        using var forwarder = new LiveKitStructuredLogForwarder(
            resource,
            resourceLoggerService,
            configuration,
            NullLogger<LiveKitStructuredLogForwarder>.Instance);
        var cancellationToken = TestContext.Current.CancellationToken;
        var exportTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await context.Request.InputStream.CopyToAsync(Stream.Null, cancellationToken);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Close();
            return context.Request.HttpMethod;
        });

        await forwarder.StartAsync(TestContext.Current.CancellationToken);
        resourceLoggerService.GetLogger(resource).LogInformation("structured log for coverage");
        resourceLoggerService.Complete(resource);

        await forwarder.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        Assert.Equal("POST", await exportTask);
    }

    private static async Task AssertFailsAfterSetupAsync(
        string endpoint,
        params (string Key, string Value)[] values) =>
        await AssertFailsAfterSetupAsync(endpoint, new NullReferenceException(), values);

    private static async Task AssertFailsAfterSetupAsync(
        string endpoint,
        Exception expectedException,
        params (string Key, string Value)[] values)
    {
        var settings = values.ToDictionary(value => value.Key, value => (string?)value.Value);
        settings["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"] = endpoint;
        var resource = DistributedApplicationBuilderTestHost.CreateBuilder().AddLiveKitServer("livekit").Resource;
        using var forwarder = new LiveKitStructuredLogForwarder(
            resource,
            null!,
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            NullLogger<LiveKitStructuredLogForwarder>.Instance);

        await forwarder.StartAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAnyAsync<Exception>(async () => await forwarder.ExecuteTask!);
        Assert.IsType(expectedException.GetType(), exception);
    }
}
