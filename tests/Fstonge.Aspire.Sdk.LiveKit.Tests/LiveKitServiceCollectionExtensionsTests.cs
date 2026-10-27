using Fstonge.Aspire.Sdk.LiveKit;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Sdk.LiveKit.Tests;

public sealed class LiveKitServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLiveKitSdk_UsesRootEnvironmentContractAndRegistersRoomClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LIVEKIT_URL"] = "http://localhost:7880",
                ["LIVEKIT_API_KEY"] = "local-key",
                ["LIVEKIT_API_SECRET"] = "01234567890123456789012345678901"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLiveKitSdk(configuration);

        using var provider = services.BuildServiceProvider();

        var firstClient = provider.GetRequiredService<RoomServiceClient>();
        var secondClient = provider.GetRequiredService<RoomServiceClient>();

        Assert.NotSame(firstClient, secondClient);
    }

    [Fact]
    public void AddLiveKitSdk_BindsNamedConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LiveKit:Url"] = "https://livekit.example.test",
                ["LiveKit:ApiKey"] = "section-key",
                ["LiveKit:ApiSecret"] = "01234567890123456789012345678901"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLiveKitSdk(configuration);

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<LiveKitOptions>>().Value;
        Assert.Equal("https://livekit.example.test", options.Url);
        Assert.Equal("section-key", options.ApiKey);
    }

    [Fact]
    public void AddLiveKitSdk_ReportsMissingSettingsWithoutSecretValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LIVEKIT_API_SECRET"] = "must-not-appear-in-errors"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLiveKitSdk(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<RoomServiceClient>());

        Assert.Contains("LIVEKIT_URL", exception.Message);
        Assert.Contains("LIVEKIT_API_KEY", exception.Message);
        Assert.DoesNotContain("must-not-appear-in-errors", exception.Message);
    }

    [Fact]
    public void AddLiveKitSdk_RejectsShortApiSecretsWithoutShowingTheirValues()
    {
        const string shortSecret = "too-short-secret";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LIVEKIT_URL"] = "http://localhost:7880",
                ["LIVEKIT_API_KEY"] = "local-key",
                ["LIVEKIT_API_SECRET"] = shortSecret
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLiveKitSdk(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<RoomServiceClient>());

        Assert.Contains("at least 32 bytes", exception.Message);
        Assert.DoesNotContain(shortSecret, exception.Message);
    }

    [Theory]
    [InlineData("ws://localhost:7880")]
    [InlineData("not-a-url")]
    public void AddLiveKitSdk_RejectsNonHttpServerUrls(string url)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LIVEKIT_URL"] = url,
                ["LIVEKIT_API_KEY"] = "local-key",
                ["LIVEKIT_API_SECRET"] = "01234567890123456789012345678901"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLiveKitSdk(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<RoomServiceClient>());

        Assert.Contains("HTTP or HTTPS", exception.Message);
    }
}
