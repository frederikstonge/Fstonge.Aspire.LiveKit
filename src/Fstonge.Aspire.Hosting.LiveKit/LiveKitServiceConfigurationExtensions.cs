using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

public static partial class LiveKitHostingExtensions
{
    public static IResourceBuilder<LiveKitServerResource> WithLiveKitServerConfiguration(
        this IResourceBuilder<LiveKitServerResource> livekit,
        LiveKitServerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentNullException.ThrowIfNull(configuration);
        var topLevelConfiguration = configuration.ToDictionary();
        var rtcConfiguration = configuration.ToRtcDictionary();
        if (topLevelConfiguration.Count == 0 && rtcConfiguration.Count == 0)
        {
            throw new InvalidOperationException("At least one LiveKit server configuration setting must be provided.");
        }

        livekit.Resource.Configuration = configuration;
        return livekit;
    }

    public static IResourceBuilder<LiveKitIngressResource> WithIngressConfiguration(
        this IResourceBuilder<LiveKitIngressResource> ingress,
        LiveKitIngressConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(ingress);
        ArgumentNullException.ThrowIfNull(configuration);
        _ = configuration.ToYaml(ingress.Resource.JsonLoggingEnabled);
        ingress.Resource.Configuration = configuration;
        return ingress;
    }

    public static IResourceBuilder<LiveKitEgressResource> WithEgressConfiguration(
        this IResourceBuilder<LiveKitEgressResource> egress,
        LiveKitEgressConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(egress);
        ArgumentNullException.ThrowIfNull(configuration);
        _ = configuration.ToYaml(egress.Resource.JsonLoggingEnabled);
        egress.Resource.Configuration = configuration;
        return egress;
    }

    public static IResourceBuilder<LiveKitSipResource> WithSipConfiguration(
        this IResourceBuilder<LiveKitSipResource> sip,
        LiveKitSipConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(sip);
        ArgumentNullException.ThrowIfNull(configuration);
        _ = configuration.ToYaml(sip.Resource.JsonLoggingEnabled);
        sip.Resource.Configuration = configuration;
        return sip;
    }
}
