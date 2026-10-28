using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Fstonge.Aspire.Hosting.LiveKit;

public static partial class LiveKitHostingExtensions
{
    /// <summary>
    /// Applies typed configuration settings to the LiveKit server.
    /// </summary>
    /// <param name="livekit">The LiveKit server resource builder.</param>
    /// <param name="configure">A delegate to configure the <see cref="LiveKitServerConfiguration"/>.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitServerResource}"/> with the configuration applied.</returns>
    public static IResourceBuilder<LiveKitServerResource> WithLiveKitServerConfiguration(
        this IResourceBuilder<LiveKitServerResource> livekit,
        Action<LiveKitServerConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(livekit);
        ArgumentNullException.ThrowIfNull(configure);
        var current = livekit.Resource.Configuration;
        var configuration = current is null
            ? new LiveKitServerConfiguration()
            : new LiveKitServerConfiguration
            {
                LogLevel = current.LogLevel,
                Rtc = new LiveKitServerRtcConfiguration
                {
                    UseExternalIp = current.Rtc.UseExternalIp,
                    PortRangeStart = current.Rtc.PortRangeStart,
                    PortRangeEnd = current.Rtc.PortRangeEnd,
                },
            };
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

    /// <summary>
    /// Applies typed configuration settings to the LiveKit Ingress service.
    /// </summary>
    /// <param name="ingress">The LiveKit Ingress resource builder.</param>
    /// <param name="configure">A delegate to configure the <see cref="LiveKitIngressConfiguration"/>.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitIngressResource}"/> with the configuration applied.</returns>
    public static IResourceBuilder<LiveKitIngressResource> WithIngressConfiguration(
        this IResourceBuilder<LiveKitIngressResource> ingress,
        Action<LiveKitIngressConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(ingress);
        ArgumentNullException.ThrowIfNull(configure);
        var current = ingress.Resource.Configuration;
        var configuration = current is null
            ? new LiveKitIngressConfiguration()
            : new LiveKitIngressConfiguration
            {
                CpuCost = new LiveKitIngressCpuCostConfiguration
                {
                    RtmpCpuCost = current.CpuCost.RtmpCpuCost,
                    WhipCpuCost = current.CpuCost.WhipCpuCost,
                    WhipBypassTranscodingCpuCost = current.CpuCost.WhipBypassTranscodingCpuCost,
                    UrlCpuCost = current.CpuCost.UrlCpuCost,
                    MinIdleRatio = current.CpuCost.MinIdleRatio,
                },
                Logging = new LiveKitServiceLoggingConfiguration { Level = current.Logging.Level },
            };
        configure(configuration);
        _ = configuration.ToYaml(ingress.Resource.JsonLoggingEnabled);
        ingress.Resource.Configuration = configuration;
        return ingress;
    }

    /// <summary>
    /// Applies typed configuration settings to the LiveKit Egress service.
    /// </summary>
    /// <param name="egress">The LiveKit Egress resource builder.</param>
    /// <param name="configure">A delegate to configure the <see cref="LiveKitEgressConfiguration"/>.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitEgressResource}"/> with the configuration applied.</returns>
    public static IResourceBuilder<LiveKitEgressResource> WithEgressConfiguration(
        this IResourceBuilder<LiveKitEgressResource> egress,
        Action<LiveKitEgressConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(egress);
        ArgumentNullException.ThrowIfNull(configure);
        var current = egress.Resource.Configuration;
        var configuration = current is null
            ? new LiveKitEgressConfiguration()
            : new LiveKitEgressConfiguration
            {
                TemplatePort = current.TemplatePort,
                DebugHandlerPort = current.DebugHandlerPort,
                PulseSinkReapGraceSeconds = current.PulseSinkReapGraceSeconds,
                TemplateBase = current.TemplateBase,
                EnableChromeSandbox = current.EnableChromeSandbox,
                Logging = new LiveKitServiceLoggingConfiguration { Level = current.Logging.Level },
            };
        configure(configuration);
        _ = configuration.ToYaml(egress.Resource.JsonLoggingEnabled);
        egress.Resource.Configuration = configuration;
        return egress;
    }

    /// <summary>
    /// Applies typed configuration settings to the LiveKit SIP service.
    /// </summary>
    /// <param name="sip">The LiveKit SIP resource builder.</param>
    /// <param name="configure">A delegate to configure the <see cref="LiveKitSipConfiguration"/>.</param>
    /// <returns>The <see cref="IResourceBuilder{LiveKitSipResource}"/> with the configuration applied.</returns>
    public static IResourceBuilder<LiveKitSipResource> WithSipConfiguration(
        this IResourceBuilder<LiveKitSipResource> sip,
        Action<LiveKitSipConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(sip);
        ArgumentNullException.ThrowIfNull(configure);
        var current = sip.Resource.Configuration;
        var configuration = current is null
            ? new LiveKitSipConfiguration()
            : new LiveKitSipConfiguration
            {
                ClusterId = current.ClusterId,
                MaxCpuUtilization = current.MaxCpuUtilization,
                MaxActiveCalls = current.MaxActiveCalls,
                UseExternalIp = current.UseExternalIp,
                LocalNet = current.LocalNet,
                Nat1To1Ip = current.Nat1To1Ip,
                MediaUseExternalIp = current.MediaUseExternalIp,
                MediaNat1To1Ip = current.MediaNat1To1Ip,
                UdpMaxPayload = current.UdpMaxPayload,
                EnableJitterBuffer = current.EnableJitterBuffer,
                EnableJitterBufferProbability = current.EnableJitterBufferProbability,
                Logging = new LiveKitServiceLoggingConfiguration { Level = current.Logging.Level },
            };
        configure(configuration);
        _ = configuration.ToYaml(sip.Resource.JsonLoggingEnabled);
        sip.Resource.Configuration = configuration;
        return sip;
    }
}
