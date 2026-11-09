using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fstonge.Aspire.Rtc.LiveKit;

/// <summary>
/// Extension methods for setting up LiveKit RTC client services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class LiveKitRtcServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ILiveKitRtcRoomFactory"/> and configures <see cref="LiveKitRtcOptions"/> from configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The configuration instance to bind options from.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddLiveKitRtcSdk(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LiveKitRtcOptions>()
            .Configure(options =>
            {
                configuration.GetSection(LiveKitRtcOptions.SectionName).Bind(options);
                options.Url = configuration["LIVEKIT_URL"] ?? options.Url;
            })
            .Validate(options => LiveKitRtcRoomFactory.IsValidServerUrl(options.Url),
                "LIVEKIT_URL must be an absolute HTTP, HTTPS, WS, or WSS URL without credentials, query, or fragment.")
            .ValidateOnStart();

        services.AddSingleton<ILiveKitRtcRoomFactory, LiveKitRtcRoomFactory>();

        return services;
    }
}
