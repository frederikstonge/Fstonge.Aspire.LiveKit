using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fstonge.Aspire.Rtc.LiveKit;

public static class LiveKitRtcServiceCollectionExtensions
{
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
