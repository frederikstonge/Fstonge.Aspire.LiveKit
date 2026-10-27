# Plan 1: LiveKit Server, Ingress, and .NET SDK Wrapper

## Objective

Deliver the first usable slice of the LiveKit Aspire integration: the core
LiveKit server, opt-in Ingress, and a .NET library that configures the upstream
server SDK from the same settings exposed by Aspire. Egress and SIP are
intentionally left for [Plan 2](./livekit-egress-sip-plan.md).

## Scope and boundaries

- Add an Aspire hosting package for the LiveKit server and its optional
  Ingress companion resource.
- Add the .NET wrapper package over `livekit-server-sdk-dotnet`.
- Use Aspire's existing Redis integration; do not add a second Redis resource.
- Keep server URL, API key, and API secret consistent across AppHost references
  and normal .NET configuration; inject credentials into consuming projects
  only through an explicit trusted-backend reference.
- Support embedded TURN configuration on the server; do not add a TURN
  container.
- Keep Ingress distinct from the server and opt-in. Server references must not
  imply that Ingress publishing endpoints are available.
- Do not provision production credentials, public routing, or external
  infrastructure.

## Implementation sequence

### 1. Confirm repository and upstream compatibility

- Inspect the repository's target .NET/Aspire versions, package conventions,
  test setup, and build/CI commands.
- Verify the upstream SDK's current package/API surface, service lifetimes, and
  representative operations.
- Verify the official server and Ingress images, supported configuration
  formats, required ports, health behavior, Redis requirements, and version/tag
  conventions.
- Confirm supported Aspire APIs for container resources, Redis references,
  service discovery, parameters/secrets, endpoint bindings, and project
  references.
- Record decisions where the spec leaves choices open: package names, typed
  configuration surface, local credential policy, raw-configuration
  precedence, and portable UDP/RTC bindings.

**Exit check:** supported versions and the configuration/endpoint design are
clear enough to implement and test without relying on undocumented image or
Aspire behavior.

### 2. Establish packages and shared connection contract

- Create the hosting integration package (working name
  `Fstonge.Aspire.Hosting.LiveKit`) and the SDK wrapper library (working name
  `Fstonge.Aspire.Sdk.LiveKit`) using repository conventions.
- Define and document the shared `LIVEKIT_URL`, `LIVEKIT_API_KEY`, and
  `LIVEKIT_API_SECRET` contract.
- Ensure the host-reachable URL is injected for host-running consumers and a
  usable service-discovery URL is available to containerized consumers, using
  supported Aspire conventions. Treat an explicit resource reference as the
  opt-in point for secret properties.
- Keep secrets in Aspire parameters or another secret-aware source; never place
  secret values in resource names, labels, URLs, logs, or exception messages.
- Define actionable validation for missing/invalid settings without a
  development-credential fallback in the SDK wrapper.

**Exit check:** an AppHost reference and a non-Aspire .NET configuration source
can both supply the same three required settings.

### 3. Implement the core LiveKit server resource

- Add a discoverable `AddLiveKit`-style API with the official server image,
  defaulting only an omitted version to `latest`; preserve explicit tags.
- Expose typed APIs for credentials, Redis (Aspire resource or caller-supplied
  connection), server options, embedded TURN, and relevant container settings.
- Provide sensible local-development defaults only after verifying the image's
  required configuration. Keep any local-only credential default explicit and
  documented.
- Configure and expose the API endpoint and document configurable HTTP,
  WebRTC/RTC, and embedded TURN port mappings and limitations.
- Provide a documented advanced/raw configuration escape hatch with
  deterministic typed-versus-raw precedence and clear conflict validation.
- Preserve Aspire lifecycle, health, dashboard, and service-discovery behavior.

**Exit check:** the server runs with verified local configuration, and the
resource's advertised URL and mapped ports match its actual protocol and
listener configuration.

### 4. Implement opt-in Ingress

- Add a separate `AddIngress`-style resource using the official Ingress image
  and an independently selectable version, defaulting to `latest`.
- Require an explicit shared Redis connection when Ingress is enabled; wire the
  same connection to both server and Ingress rather than starting an implicit,
  disconnected Redis instance.
- Configure the Ingress server URL and API credentials from the server
  resource/configuration contract.
- Expose typed configuration for RTMP/WHIP public base URLs, listener ports,
  optional health port, and supported container settings.
- Keep publishing URLs distinct from the SDK's LiveKit URL and from internal
  service-discovery endpoints. Do not infer public URLs from container
  networking.
- Document that public RTMP/WHIP publishing requires externally reachable
  routing and port mappings.

**Exit check:** applications without Ingress do not create or configure it;
when enabled, both services use the same Redis and Ingress advertises explicitly
configured public URLs.

### 5. Implement the .NET SDK wrapper

- Bind the shared configuration contract to typed options and validate the URL,
  key, and secret with actionable, secret-safe errors.
- Register upstream SDK client/service types through public APIs and follow the
  upstream library's lifetime guidance.
- Keep the wrapper independent of Aspire and avoid duplicating upstream
  operations or exposing AppHost types.
- Add a representative usage example based on a supported upstream operation.
- Document consuming the same settings from Aspire, environment variables,
  user secrets, and deployment configuration.

**Exit check:** options bind and validate correctly and the upstream service
resolves through dependency injection in both Aspire-configured and ordinary
.NET applications.

### 6. Verify, document, and package

- Add hosting tests for default/explicit image tags, server settings and
  overrides, endpoint/credential references, Redis propagation, Ingress
  opt-in/version/public URLs/ports, embedded TURN, and raw-configuration
  precedence.
- Add wrapper tests for binding, missing/invalid settings, secret-safe errors,
  DI registration, and the supported client lifetime.
- Add AppHost and consuming-project examples; document local credentials,
  mutable `latest`, required ports, external reachability, and known Aspire
  networking limitations.
- Run the repository's focused tests, build, and package validation.

## Acceptance criteria

1. The core server can be added with the default `latest` tag or an exact
   caller-selected tag.
2. The core server is usable without starting Ingress, and its API endpoint,
   RTC requirements, and embedded TURN settings are configurable and
   documented.
3. Ingress is a separate opt-in resource with its own image tag, explicit
   public RTMP/WHIP URLs, and the same configured Redis connection as the
   server.
4. An explicit trusted-backend resource reference receives the documented
   server URL and credentials in a form usable from its hosting context;
   credentials are not distributed to projects that merely need the public
   server endpoint, and secrets are never logged.
5. The SDK wrapper configures the upstream SDK from standard .NET
   configuration, validates required values clearly, and has no compile-time
   dependency on the AppHost.
6. Tests and examples cover the hosting and wrapper behavior in the
   implementation sequence.

## Risks and guardrails

- Do not assume exposing the server HTTP port enables WebRTC, RTMP, or TURN
  reachability; verify listeners and document external routing requirements.
- Do not hard-code container-only hostnames into host-running project settings.
- Do not assume typed configuration keys or image behavior without checking the
  selected LiveKit image version.
- Treat credentials as secrets throughout parameter binding, generated
  environment, logs, and diagnostics.
- Keep the wrapper focused on SDK configuration; the hosting package owns
  containers and their runtime settings.
