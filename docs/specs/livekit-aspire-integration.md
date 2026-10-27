# LiveKit .NET Aspire Integration Specification

## Status

Proposed

## Summary

Build a .NET Aspire integration for running LiveKit services and consuming them
from .NET projects. LiveKit is not a single-container deployment: the core
server can be accompanied by separate services: Ingress accepts RTMP/WHIP
streams, Egress exports recordings or streams, and SIP bridges telephony. These
services run as separate containers and use Redis to communicate with the
server. The integration consists of:

1. An Aspire hosting package that models the core LiveKit server and optional
   companion services as separate resources, supports default and explicitly
   selected image versions, and makes connection settings available to
   referencing projects.
2. A .NET wrapper package around
   [livekit-server-sdk-dotnet](https://github.com/pabloFuente/livekit-server-sdk-dotnet)
   that configures the upstream SDK from those settings.

The hosting integration covers the core server and optional Ingress, Egress,
and SIP resources. Each companion service is opt-in. The intent is to make local
development straightforward while keeping the connection contract usable by
deployed Aspire applications.

## Goals

- Add the LiveKit server to an Aspire AppHost with a small, discoverable API.
- Optionally add LiveKit Ingress as a distinct container resource and configure
  its required Redis and server connections.
- Optionally add LiveKit Egress and SIP as distinct container resources, each
  with its own image, configuration, networking, and shared Redis/server
  connections.
- Allow the AppHost to declare Redis with Aspire's existing Redis hosting
  integration and pass its connection to the LiveKit server and enabled
  companion services.
- Expose LiveKit's embedded TURN configuration through the AppHost API without
  requiring a separate TURN container for the standard setup.
- Use the appropriate image for each service, defaulting to `latest`, and allow
  callers to select versions.
- Let AppHost authors configure each container and its LiveKit-specific settings
  directly in the AppHost project through a discoverable fluent API.
- Configure and expose the server URL, API key, and API secret to projects
  referencing the LiveKit resource.
- Provide a typed .NET wrapper that reads the injected settings and configures
  the upstream server SDK.
- Keep credentials out of source control and make configuration failures clear.

## Non-goals

- Implement or replace the LiveKit server.
- Implement browser, mobile, or other client SDKs.
- Define production deployment, reverse-proxy, TLS, or external load-balancer
  configuration.
- Automatically provision production credentials or a LiveKit Cloud account.
- Hide or fork the upstream .NET SDK.

## Proposed package and project boundaries

### Aspire hosting integration

Create a hosting integration package (working name
`Fstonge.Aspire.Hosting.LiveKit`) that is referenced by the AppHost. It owns:

- The core LiveKit server resource and its image configuration.
- Optional companion service resources for LiveKit Ingress, Egress, and SIP,
  each using its own image, configuration, ports, and lifecycle.
- Default and caller-selected image tags, independently configurable per
  service.
- AppHost-level configuration for service settings, container options, and
  endpoints, without requiring users to hand-author and mount configuration
  files for common scenarios.
- The connections between services and any required supporting resources, such
  as Redis for Ingress.
- Using an Aspire Redis resource declared by the AppHost and passing its
  connection to the server and any enabled companion services.
- Supplying the appropriate endpoints and connection settings to projects that
  reference each resource.

The AppHost API should follow normal Aspire hosting conventions. A representative
shape is:

```csharp
var redis = builder.AddRedis("redis");

var apiKey = builder.AddParameter("livekit-api-key", secret: true);
var apiSecret = builder.AddParameter("livekit-api-secret", secret: true);

var livekit = builder.AddLiveKit("livekit");
livekit.WithApiKey(apiKey)
    .WithApiSecret(apiSecret)
    .WithRedis(redis)
    .WithServerOptions(options =>
    {
        options.Rtc.TcpPort = 7881;
        options.Turn.Enabled = true;
    });

var ingress = livekit.AddIngress("ingress");
ingress.WithPublicUrls(
    rtmpBaseUrl: "rtmp://localhost:1935/x",
    whipBaseUrl: "http://localhost:8080/w");

var egress = livekit.AddEgress("egress")
    .WithStorage(storage => storage.UseLocalDirectory("./recordings"));

var sip = livekit.AddSip("sip");

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(livekit);
```

This is illustrative, not a prescribed API signature. Use Aspire's existing
Redis hosting integration rather than introducing a LiveKit-specific Redis
resource. The LiveKit hosting API should accept the Redis resource (or a
caller-supplied Redis connection) and translate it into the configuration
format expected by the server and enabled companion services.

Ingress should be opt-in: applications that only need the core server should
not start or configure it. The exact resource types and fluent method names
should be confirmed against the Aspire version and existing project conventions
during implementation. The API should make clear which resource a project
references; referencing the server should not accidentally imply access to
Ingress's public RTMP/WHIP endpoints.

The example is illustrative, not a prescribed API signature. The implemented
API should let AppHost authors configure, per service:

- Supported LiveKit settings through typed configuration callbacks or fluent
  methods, including server keys, RTC ports, Redis, embedded TURN, Ingress
  public URLs, Egress output, and SIP/RTP listener settings.
- Container-level settings such as image tag, environment variables, command
  arguments, port bindings, volumes/config mounts, and health checks where
  supported by Aspire.
- Optional Ingress, Egress, and SIP resources and their dependencies, including
  choosing or supplying the shared Redis resource.
- Embedded TURN configuration for the LiveKit server, including enablement,
  advertised ports/domain, and TLS certificate/key configuration when
  supported.

Common settings should have typed, discoverable APIs and flow into valid
container configuration automatically. Preserve an escape hatch for advanced or
new upstream settings (for example, a configuration object or mounted config
file) so users are not blocked while waiting for the integration to wrap every
LiveKit option. Establish clear precedence when the same setting is supplied
through both a typed API and a raw configuration source, and fail clearly on
conflicting or invalid configuration rather than silently ignoring it.

### SDK wrapper

Create a .NET library package (working name `Fstonge.Aspire.Sdk.LiveKit`) that
references the upstream `livekit-server-sdk-dotnet` package. It owns:

- A typed options/configuration contract for the LiveKit URL, API key, and API
  secret.
- Dependency-injection registration for the upstream SDK's relevant clients or
  services.
- Validation of required settings at registration or first use, with actionable
  errors.
- Documentation and tests showing configuration from Aspire-injected settings.

The wrapper should adapt the upstream SDK rather than duplicate its API. Keep
the wrapper's public surface limited to configuration and DI conveniences unless
the upstream SDK requires additional adaptation. It should use generic
Microsoft.Extensions configuration and dependency-injection packages rather
than an ASP.NET Core framework reference, so it remains usable from console,
worker, and ASP.NET Core projects.

## Configuration and connection contract

The hosting integration and wrapper must agree on one configuration contract.
The initial proposed contract is:

| Setting | Purpose | Required |
|---|---|---|
| `LIVEKIT_URL` | Server URL used by the SDK | Yes |
| `LIVEKIT_API_KEY` | Server-side API key | Yes |
| `LIVEKIT_API_SECRET` | Server-side API secret | Yes |

Use the Aspire resource's service-discovery endpoint for the URL where
appropriate. The URL must be usable from the referencing container or process;
do not inject a container-only hostname into a host-running project.

The AppHost integration must make the endpoint available to projects that
reference the LiveKit resource. API keys and secrets are caller-provided
credentials configured on the self-hosted LiveKit server; LiveKit does not
issue them automatically. Keep them in secret Aspire parameters and inject them
only into explicitly selected trusted server-side consumers. Do not distribute
the API secret to browser or other untrusted clients; issue short-lived access
tokens instead. The SDK wrapper must support standard .NET configuration so
the same settings can be supplied outside Aspire (for example, from environment
variables, user secrets, or deployment configuration). The wrapper must not
require a compile-time reference to the AppHost project.

The variable names above are the proposed initial contract, not an assertion
about the upstream SDK's required option names. Map them to the upstream SDK
through typed options and document any necessary naming differences. Do not log
the API secret.

## Hosting behavior

- Default the image to the official LiveKit server image with the `latest` tag.
- Provide an explicit way to select a version/tag, for example
  `AddLiveKit(name, version: "...")`.
- Treat an omitted version as `latest`; do not silently replace an explicitly
  requested version.
- Configure the container through AppHost APIs, with sensible local-development
  defaults for API credentials and required listener/RTC ports.
- Allow the AppHost to override LiveKit server settings and relevant container
  properties without requiring users to edit integration internals.
- Make the primary server endpoint available to Aspire service discovery.
- Document any additional ports that must be exposed for WebRTC/RTC traffic and
  allow callers to override port mappings when required.
- Ensure that the endpoint advertised to referencing projects matches the
  actual configured protocol, host, and port.
- Preserve normal Aspire container lifecycle, health, and dashboard visibility
  behavior.

The exact LiveKit server configuration format, required ports, and health check
must be verified against the selected LiveKit server image during implementation.
Avoid assuming that exposing the HTTP API port alone is sufficient for media
traffic.

### Optional Ingress service

- Run Ingress as a separate resource using its own official image; do not model
  it as a mode or extra port on the LiveKit server container.
- Make Ingress opt-in. It is needed for workflows that accept RTMP or WHIP
  input, not for every LiveKit deployment.
- Configure its LiveKit server URL and API credentials, and connect it to the
  same Redis instance used by the LiveKit server.
- When Ingress is enabled, configure that Redis connection on both the LiveKit
  server and Ingress. A server-only, single-node local setup may omit Redis if
  supported by the selected server configuration.
- Support either an Aspire-managed Redis resource or a caller-provided Redis
  connection. The integration must not silently start a second Redis instance
  that is disconnected from the server.
- Configure the LiveKit server's Ingress base URLs to advertise the actual
  externally reachable RTMP/WHIP endpoint. These are distinct from the server
  SDK's WebSocket URL and must not be inferred from a container-internal
  endpoint.
- Document and configure Ingress's RTMP and WHIP listener ports, health port
  when enabled, and public endpoint/port-mapping requirements.
- Use the selected Ingress image version independently from the LiveKit server
  image version.
- Allow the AppHost to configure Ingress settings and container properties,
  including Redis, server credentials, public base URLs, listener ports, and
  health settings.

Ingress requires a publicly reachable publishing endpoint in deployments that
receive streams. Local container networking alone does not make that endpoint
reachable to external encoders. Document this limitation and avoid claiming
end-to-end RTMP/WHIP availability unless the advertised endpoint and routing
are configured.

### Optional Egress service

- Run Egress as its own opt-in resource using its own official image and an
  independently selectable image tag.
- Configure its LiveKit server URL, API credentials, and the same Redis
  instance used by the server.
- Expose AppHost configuration for common Egress options, including health and
  metrics ports and supported resource limits.
- Allow configuration of recording and streaming output destinations, with
  required credentials supplied through secret-aware Aspire parameters.
- Support a documented local-development output option where the container can
  access the target path. Document that local file output requires an
  appropriate volume or bind mount, and cloud/object storage output needs its
  own provider configuration.
- Avoid logging storage secrets or implying Egress is usable until an output
  destination is configured.

### Optional SIP service

- Run SIP as its own opt-in resource using its own official image and an
  independently selectable image tag.
- Configure its LiveKit server URL, API credentials, and the same Redis
  instance used by the server.
- Expose AppHost configuration for SIP and RTP listener ports, health and
  metrics ports when enabled, and relevant container networking options.
- Document that SIP requires public reachability and suitable UDP/TCP routing
  for the configured SIP and RTP ports. Local container networking alone does
  not provide telephony connectivity.
- Allow the AppHost author to supply SIP provider/trunk settings and credentials
  securely. Do not automatically create provider accounts, trunks, or dispatch
  rules; those are server-side resources configured through the LiveKit APIs.

### Redis and TURN

- Redis may be omitted for a standalone, single-node LiveKit server if supported
  by its configuration, but is required when using companion services such as
  Ingress, Egress, or SIP. When Redis is used, allow the AppHost to pass an
  Aspire Redis resource and ensure the server and all enabled companion
  services use that same instance.
- Use Aspire's existing Redis hosting integration (`AddRedis`) to declare the
  Redis container, including its standard dashboard, health, and connection
  reference behavior. The LiveKit package provides service-specific wiring,
  not a duplicate Redis integration.
- LiveKit has an embedded TURN server. Expose its supported configuration
  through the AppHost API and document public ports, domain, and certificate
  requirements. Do not add a separate TURN container for the standard embedded
  TURN scenario.
- No dedicated Aspire TURN/coturn integration was found during specification
  research. Allow callers to configure an externally managed TURN endpoint if
  needed. A separate coturn resource can be considered later, but should not be
  implied by enabling LiveKit's embedded TURN.
- TURN reachability depends on public networking and correct advertised
  addresses and ports; local container networking alone is not sufficient for
  external clients.

## Credentials and local development

- Support caller-supplied API key and secret.
- Provide a documented local-development default only if it is safe, clearly
  scoped to development, and cannot be mistaken for production credentials.
- Prefer Aspire parameters or another secret-aware mechanism for credentials
  rather than embedding secrets in project files.
- Make it possible to provide credentials through the normal Aspire parameter
  flow without exposing the secret in logs, resource labels, or generated
  connection URLs.
- Never generate or publish production credentials automatically.

If the integration chooses to offer a convenience default credential for local
containers, it must be explicitly documented as local-only and must not be used
by the SDK wrapper as a fallback when a required setting is missing.

## SDK wrapper behavior

- Bind the agreed configuration contract to a typed options object.
- Validate the server URL, API key, and API secret and report which setting is
  missing without including secret values.
- Register the upstream SDK's supported client/service types using its public
  APIs.
- Follow the upstream SDK's lifetime guidance; avoid registering non-thread-safe
  clients as singletons.
- Provide an example consuming the registered SDK service to perform a
  representative server-side operation, such as creating a room or generating
  an access token, subject to the upstream SDK's capabilities.
- Keep configuration independent from Aspire so the library remains usable in
  non-Aspire applications.

## Compatibility and versioning

- Target the .NET and Aspire versions supported by the repository.
- Pin a compatible upstream SDK dependency range and verify compatibility when
  upgrading it.
- Allow selecting a LiveKit server image version independently of the wrapper
  package version.
- Allow the Ingress image version to be selected independently of the server
  image version.
- Allow Egress and SIP image versions to be selected independently of the
  server and each other.
- Use Aspire's existing Redis hosting integration to declare Redis and pass its
  connection into LiveKit services that need it.
- Configure LiveKit's embedded TURN server through the server resource; do not
  add a TURN image unless a separate TURN service is explicitly scoped.
- Document that `latest` is mutable and recommend an explicit image version for
  repeatable CI and deployment environments.

Ingress, Egress, and SIP are optional companion services; adding the core server
must not implicitly start any of them. Agent workers and third-party storage,
telephony providers, and TURN servers remain caller-managed workloads or
services, not containers automatically provisioned by this integration.

## Acceptance criteria

1. An AppHost can add the core LiveKit server resource without specifying an
   image version, and the resulting image reference uses `latest`.
2. An AppHost can select a specific server image tag, and the resource uses
   exactly that tag.
3. The server starts with valid local-development configuration and its
   required API and RTC ports are documented and configurable as appropriate.
4. Ingress is an optional, separate resource with an independently selectable
   image tag; adding it configures its Redis/server connections and documents
   its public RTMP/WHIP endpoint and required ports.
5. Egress is an optional, separate resource with an independently selectable
   image tag; it receives the server/Redis configuration and supports documented
   output destination and volume configuration.
6. SIP is an optional, separate resource with an independently selectable image
   tag; it receives the server/Redis configuration and documents SIP/RTP
   listener and public networking requirements.
7. An AppHost can declare Redis using Aspire's Redis hosting integration and
   pass that resource to LiveKit; the server and all enabled companion services
   use the same Redis connection.
8. AppHost authors can configure LiveKit's embedded TURN behavior and required
   advertised endpoints/ports through the server resource; no separate TURN
   container is required for this scenario.
9. AppHost authors can set and override supported server and companion-service
   settings directly in code, including relevant container configuration,
   without hand-authoring configuration files for common scenarios.
10. Advanced settings not yet represented by typed APIs can be supplied through
   a documented configuration-file or raw-configuration escape hatch, with
   deterministic precedence and validation.
11. A referencing project receives a usable LiveKit URL and the configured API
   credentials through the documented connection contract.
12. A project using the wrapper can register and resolve the upstream SDK
   integration using configuration supplied by Aspire.
13. The wrapper also works when settings are supplied through standard .NET
   configuration without Aspire.
14. Missing or invalid required settings produce clear errors, and secret values
   are not written to logs or error messages.
15. Automated tests cover default and explicit image tags for each service,
   AppHost configuration and override behavior, Redis connection propagation,
   Egress output configuration, SIP port configuration, embedded TURN
   configuration, options binding/validation, and SDK registration.
16. Documentation includes AppHost and consuming-project examples, local
    credential guidance, service-specific port and endpoint requirements, and
    the mutable nature of `latest`.

## Implementation notes and decisions to confirm

- Confirm the repository's target .NET/Aspire versions and package naming before
  implementation.
- Verify the upstream SDK's current configuration types, supported service
  lifetimes, and operations before defining the wrapper API.
- Verify the official server, Ingress, Egress, and SIP images' configuration
  mechanisms, required ports, and health-check behavior.
- Verify the supported Aspire Redis resource reference APIs and how to map the
  resulting connection to LiveKit's Redis configuration.
- Verify LiveKit's embedded TURN options and certificate/port requirements.
- Determine how AppHost configuration expresses Ingress's externally reachable
  RTMP/WHIP base URLs and SIP's public listener requirements without confusing
  them with internal service-discovery addresses.
- Choose typed configuration surfaces for common server, Ingress, Egress, and
  SIP settings, plus an escape hatch for settings not yet modeled; define
  precedence when typed settings, environment variables, and mounted
  configuration overlap.
- Determine safe defaults and supported volume/storage configuration for Egress
  output.
- Decide whether local API credentials are required parameters or whether the
  integration provides an explicitly development-only default.
- Decide which RTC/UDP port mappings can be represented portably by the Aspire
  resource API; document any limitations rather than implying full media
  connectivity when only the HTTP endpoint is available.

## References

- [LiveKit deployment documentation](https://docs.livekit.io/transport/self-hosting/deployment/)
- [LiveKit Ingress documentation](https://docs.livekit.io/transport/self-hosting/ingress/)
- [LiveKit Ingress source repository](https://github.com/livekit/ingress)
- [LiveKit Egress documentation](https://docs.livekit.io/transport/self-hosting/egress/)
- [LiveKit Egress source repository](https://github.com/livekit/egress)
- [LiveKit SIP service repository](https://github.com/livekit/sip)
- [Aspire Redis integration](https://aspire.dev/integrations/caching/redis/redis-get-started/)
- [LiveKit self-hosted deployment and TURN documentation](https://docs.livekit.io/transport/self-hosting/deployment/)
