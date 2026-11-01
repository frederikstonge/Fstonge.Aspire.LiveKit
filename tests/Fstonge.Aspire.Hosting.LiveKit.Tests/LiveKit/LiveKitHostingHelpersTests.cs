using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit.Tests.LiveKit;

[Collection(DistributedApplicationBuilderTestCollection.Name)]
public sealed class LiveKitHostingHelpersTests(DistributedApplicationBuilderTestHost appHost)
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void ValidateOptionalPort_RejectsOutOfRangePort(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LiveKitHostingHelpers.ValidateOptionalPort(port, "port"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void ValidateOptionalPort_AcceptsValidPort(int port)
    {
        LiveKitHostingHelpers.ValidateOptionalPort(port, "port");
    }

    [Fact]
    public void GetJsonLoggingConfiguration_ReflectsResourceFlag()
    {
        var builder = appHost.CreateBuilder();
        var livekit = builder.AddLiveKit("livekit");

        Assert.Equal("logging:\n  json: true\n", LiveKitHostingHelpers.GetJsonLoggingConfiguration(livekit.Resource));
    }

    [Fact]
    public void GetHttpPortConfiguration_FormatsOnlyConfiguredPort()
    {
        var configured = LiveKitHostingHelpers.GetHttpPortConfiguration(8080, "health_port");
        var omitted = LiveKitHostingHelpers.GetHttpPortConfiguration(null, "health_port");

        Assert.Equal("health_port: 8080\n", configured.ToString());
        Assert.Equal(string.Empty, omitted.ToString());
    }
}
