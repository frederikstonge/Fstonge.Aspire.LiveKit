using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

public static partial class LiveKitHostingExtensions
{
    public static IResourceBuilder<LiveKitServerResource> WithLiveKitServerConfiguration(
        this IResourceBuilder<LiveKitServerResource> livekit,
        Action<LiveKitServerConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentNullException.ThrowIfNull(configure);
        var configuration = livekit.Resource.Configuration ?? new LiveKitServerConfiguration();
        configure(configuration);
        var topLevelConfiguration = configuration.ToDictionary();
        var rtcConfiguration = configuration.Rtc.ToDictionary();
        if (topLevelConfiguration.Count == 0 && rtcConfiguration.Count == 0)
        {
            throw new InvalidOperationException("At least one LiveKit server configuration setting must be provided.");
        }

        livekit.Resource.Configuration = configuration;
        return livekit;
    }

    public static IResourceBuilder<LiveKitIngressResource> WithIngressConfiguration(
        this IResourceBuilder<LiveKitIngressResource> ingress,
        Action<LiveKitIngressConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(ingress);
        ArgumentNullException.ThrowIfNull(configure);
        var configuration = ingress.Resource.Configuration ?? new LiveKitIngressConfiguration();
        configure(configuration);
        _ = configuration.ToYaml(ingress.Resource.JsonLoggingEnabled);
        ingress.Resource.Configuration = configuration;
        return ingress;
    }

    public static IResourceBuilder<LiveKitEgressResource> WithEgressConfiguration(
        this IResourceBuilder<LiveKitEgressResource> egress,
        Action<LiveKitEgressConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(egress);
        ArgumentNullException.ThrowIfNull(configure);
        var configuration = egress.Resource.Configuration ?? new LiveKitEgressConfiguration();
        configure(configuration);
        _ = configuration.ToYaml(egress.Resource.JsonLoggingEnabled);
        egress.Resource.Configuration = configuration;
        return egress;
    }

    public static IResourceBuilder<LiveKitSipResource> WithSipConfiguration(
        this IResourceBuilder<LiveKitSipResource> sip,
        Action<LiveKitSipConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(sip);
        ArgumentNullException.ThrowIfNull(configure);
        var configuration = sip.Resource.Configuration ?? new LiveKitSipConfiguration();
        configure(configuration);
        _ = configuration.ToYaml(sip.Resource.JsonLoggingEnabled);
        sip.Resource.Configuration = configuration;
        return sip;
    }
}
