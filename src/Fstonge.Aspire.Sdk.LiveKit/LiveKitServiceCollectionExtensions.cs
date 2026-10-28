using Livekit.Server.Sdk.Dotnet;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Sdk.LiveKit;

public static class LiveKitServiceCollectionExtensions
{
    public static IServiceCollection AddLiveKitSdk(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LiveKitOptions>()
            .Configure(options =>
            {
                configuration.GetSection(LiveKitOptions.SectionName).Bind(options);
                options.Url = configuration["LIVEKIT_URL"] ?? options.Url;
                options.ApiKey = configuration["LIVEKIT_API_KEY"] ?? options.ApiKey;
                options.ApiSecret = configuration["LIVEKIT_API_SECRET"] ?? options.ApiSecret;
            })
            .Validate(options => Uri.TryCreate(options.Url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "LIVEKIT_URL must be an absolute HTTP or HTTPS URL.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "LIVEKIT_API_KEY must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiSecret),
                "LIVEKIT_API_SECRET must be configured.")
            .Validate(options => options.ApiSecret is not null
                && Encoding.UTF8.GetByteCount(options.ApiSecret) >= 32,
                "LIVEKIT_API_SECRET must contain at least 32 bytes.")
            .ValidateOnStart();

        services.AddHttpClient<RoomServiceClient>()
            .AddTypedClient((httpClient, serviceProvider) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<LiveKitOptions>>().Value;
                return new RoomServiceClient(options.Url!, options.ApiKey!, options.ApiSecret!, httpClient);
            });

        services.AddSingleton<IAccessTokenFactory, LiveKitAccessTokenFactory>();

        return services;
    }
}
