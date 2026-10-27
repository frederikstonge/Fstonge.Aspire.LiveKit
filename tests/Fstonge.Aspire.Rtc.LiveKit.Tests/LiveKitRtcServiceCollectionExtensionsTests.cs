using Fstonge.Aspire.Rtc.LiveKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Rtc.LiveKit.Tests;

public sealed class LiveKitRtcServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData("http://localhost:7880", "ws://localhost:7880/")]
    [InlineData("https://livekit.example.test/path", "wss://livekit.example.test/path")]
    [InlineData("ws://localhost:7880", "ws://localhost:7880/")]
    [InlineData("wss://livekit.example.test/path", "wss://livekit.example.test/path")]
    public void AddLiveKitRtcSdk_NormalizesServerUrl(
        string configuredUrl,
        string expectedWebSocketUrl)
    {
        var services = new ServiceCollection();
        services.AddLiveKitRtcSdk(CreateConfiguration(("LIVEKIT_URL", configuredUrl)));

        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<ILiveKitRtcRoomFactory>();
        Assert.Equal(expectedWebSocketUrl, factory.ServerUrl.AbsoluteUri);
    }

    [Fact]
    public void AddLiveKitRtcSdk_BindsSectionAndRootSettingOverridesIt()
    {
        var configuration = CreateConfiguration(
            ("LiveKit:Url", "https://section.example.test"),
            ("LIVEKIT_URL", "https://root.example.test"));
        var services = new ServiceCollection();
        services.AddLiveKitRtcSdk(configuration);

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<LiveKitRtcOptions>>().Value;
        Assert.Equal("https://root.example.test", options.Url);
        Assert.Equal("wss://root.example.test/", provider
            .GetRequiredService<ILiveKitRtcRoomFactory>()
            .ServerUrl.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-url")]
    [InlineData("ftp://livekit.example.test")]
    [InlineData("https://user:password@livekit.example.test")]
    [InlineData("https://livekit.example.test?token=secret")]
    [InlineData("https://livekit.example.test#fragment")]
    public void AddLiveKitRtcSdk_RejectsInvalidServerUrls(string? url)
    {
        var services = new ServiceCollection();
        services.AddLiveKitRtcSdk(CreateConfiguration(("LIVEKIT_URL", url)));

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () =>
            {
                _ = provider.GetRequiredService<ILiveKitRtcRoomFactory>();
            });

        Assert.Contains("LIVEKIT_URL", exception.Message);
        Assert.DoesNotContain("password", exception.Message);
        Assert.DoesNotContain("secret", exception.Message);
    }

    private static IConfiguration CreateConfiguration(params (string Key, string? Value)[] values) =>
        CreateConfigurationManager(values);

    private static ConfigurationManager CreateConfigurationManager(
        IEnumerable<(string Key, string? Value)> values)
    {
        var configuration = new ConfigurationManager();
        foreach (var (key, value) in values)
        {
            configuration[key] = value;
        }

        return configuration;
    }
}
