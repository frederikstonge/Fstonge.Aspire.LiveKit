using Livekit.Server.Sdk.Dotnet;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fstonge.Aspire.Sdk.LiveKit;

/// <summary>
/// Extension methods for setting up LiveKit server SDK services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class LiveKitServiceCollectionExtensions
{
    /// <summary>
    /// Registers LiveKit server SDK services (<see cref="IRoomManagementService"/>, <see cref="IAccessTokenFactory"/>)
    /// and configures <see cref="LiveKitOptions"/> with validation.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The configuration instance to bind options from.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
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
                options.WebSocketUrl = configuration["LIVEKIT_WS_URL"] ?? options.WebSocketUrl;
                options.ApiKey = configuration["LIVEKIT_API_KEY"] ?? options.ApiKey;
                options.ApiSecret = configuration["LIVEKIT_API_SECRET"] ?? options.ApiSecret;
            })
            .Validate(options => Uri.TryCreate(options.Url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "LIVEKIT_URL must be an absolute HTTP or HTTPS URL.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.WebSocketUrl),
                "LIVEKIT_WS_URL must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "LIVEKIT_API_KEY must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiSecret),
                "LIVEKIT_API_SECRET must be configured.")
            .Validate(options => options.ApiSecret is not null
                && Encoding.UTF8.GetByteCount(options.ApiSecret) >= 32,
                "LIVEKIT_API_SECRET must contain at least 32 bytes.")
            .ValidateOnStart();

        services.AddHttpClient<IRoomManagementService, RoomManagementService>();
        services.AddSingleton<IAccessTokenFactory, LiveKitAccessTokenFactory>();

        return services;
    }
}
