using Fstonge.Aspire.Sdk.LiveKit;
using Livekit.Server.Sdk.Dotnet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Sdk.LiveKit.Tests;

public sealed class LiveKitServiceCollectionExtensionsTests
{
    [Fact]
    public void LiveKitAccessTokenFactory_CreatesTokenFromConfiguredCredentials()
    {
        var factory = new LiveKitAccessTokenFactory(Options.Create(new LiveKitOptions
        {
            ApiKey = "local-key",
            ApiSecret = "01234567890123456789012345678901"
        }));

        Assert.NotNull(factory.GetAccessToken());
    }

    [Fact]
    public async Task RoomManagementService_ForwardsRequestsToLiveKitClient()
    {
        using var httpClient = new HttpClient(new FailingHttpMessageHandler());
        var service = new RoomManagementService(
            httpClient,
            Options.Create(new LiveKitOptions
            {
                Url = "http://localhost:7880",
                ApiKey = "local-key",
                ApiSecret = "01234567890123456789012345678901"
            }));

        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateRoomAsync(new CreateRoomRequest()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.ListRoomsAsync(new ListRoomsRequest()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.DeleteRoomAsync(new DeleteRoomRequest()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateRoomMetadataAsync(new UpdateRoomMetadataRequest()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.GetParticipantAsync(new RoomParticipantIdentity()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.ListParticipantsAsync(new ListParticipantsRequest()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.MoveParticipantAsync(new MoveParticipantRequest()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.RemoveParticipantAsync(new RoomParticipantIdentity()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateParticipantAsync(new UpdateParticipantRequest()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.ForwardParticipantAsync(new ForwardParticipantRequest()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateSubscriptionsAsync(new UpdateSubscriptionsRequest()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.MutePublishedTrackAsync(new MuteRoomTrackRequest()));
        await Assert.ThrowsAnyAsync<Exception>(() => service.SendDataAsync(new SendDataRequest()));
    }

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

        var firstClient = provider.GetRequiredService<IRoomManagementService>();
        var secondClient = provider.GetRequiredService<IRoomManagementService>();

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
            () => provider.GetRequiredService<IRoomManagementService>());

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
            () => provider.GetRequiredService<IRoomManagementService>());

        Assert.Contains("at least 32 bytes", exception.Message);
        Assert.DoesNotContain(shortSecret, exception.Message);
    }

    [Fact]
    public void AddLiveKitSdk_RejectsShortApiSecretsInDevelopment()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DOTNET_ENVIRONMENT"] = "Development",
                ["LIVEKIT_URL"] = "http://localhost:7880",
                ["LIVEKIT_API_KEY"] = "local-key",
                ["LIVEKIT_API_SECRET"] = "test"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLiveKitSdk(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IRoomManagementService>());

        Assert.Contains("at least 32 bytes", exception.Message);
    }

    [Fact]
    public void AddLiveKitSdk_AcceptsApiSecretsWithAtLeast32Utf8Bytes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LIVEKIT_URL"] = "http://localhost:7880",
                ["LIVEKIT_API_KEY"] = "local-key",
                ["LIVEKIT_API_SECRET"] = new string('\u00e9', 16)
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLiveKitSdk(configuration);

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IRoomManagementService>();

        Assert.NotNull(client);
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
            () => provider.GetRequiredService<IRoomManagementService>());

        Assert.Contains("HTTP or HTTPS", exception.Message);
    }

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Expected test transport failure."));
    }
}
