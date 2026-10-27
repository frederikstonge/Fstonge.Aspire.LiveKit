# Plan 2: LiveKit Egress and SIP

## Objective

Extend the hosting integration with opt-in Egress and SIP resources after the
core server, shared configuration contract, Redis wiring, and .NET SDK wrapper
from [Plan 1](./livekit-server-ingress-plan.md) are in place.

## Scope and boundaries

- Add Egress and SIP as distinct, independently versioned container resources.
- Reuse Plan 1's LiveKit server resource, shared Redis connection, secret
  handling, service-discovery behavior, and configuration conventions.
- Extend the existing .NET wrapper only if verification of the upstream SDK
  shows a necessary configuration or DI change. Egress and SIP runtime
  containers are configured by the hosting integration; they do not require
  separate SDK wrappers by default.
- Do not automatically create object-storage buckets, telephony provider
  accounts, trunks, dispatch rules, TURN services, or public network routing.
- Do not imply that local container networking alone makes recordings,
  streaming, or telephony externally usable.

## Prerequisites

- Plan 1's hosting package and LiveKit server resource are implemented.
- The server/consumer configuration contract and secret-aware parameter
  handling are established.
- A shared Redis resource/connection can be supplied and propagated to
  companion services.
- Repository target frameworks, Aspire APIs, image configuration, and tests are
  known.

## Implementation sequence

### 1. Verify Egress and SIP runtime contracts

- Verify official Egress and SIP images, configuration mechanisms, required
  environment/configuration, health behavior, and independently selectable
  image tags.
- Confirm Egress health/metrics ports, supported resource limits, output
  destination options, and which local output paths can be mounted portably.
- Confirm SIP/RTP listener ports and protocols, optional health/metrics ports,
  and relevant public-networking requirements.
- Verify the supported Aspire APIs for port bindings, UDP/TCP exposure, volume
  mounts, health checks, and resource dependencies.
- Identify which secrets are needed for supported storage/provider examples and
  how to source them with Aspire secret-aware parameters.

**Exit check:** required service settings, ports/protocols, volume semantics,
and deployment limitations are verified against supported image versions.

### 2. Extend shared hosting abstractions only as needed

- Reuse the server resource's API credentials, server URL, Redis connection,
  image-version conventions, secret handling, and raw-configuration
  precedence.
- Add shared helper abstractions only where Egress and SIP genuinely need the
  same behavior; do not duplicate or fork Plan 1 functionality.
- Require the same explicit/shared Redis connection used by the server when
  either companion service is enabled.
- Ensure enabling either service does not implicitly enable the other.

**Exit check:** common configuration has one source of truth, and the
dependency graph clearly reflects server, Redis, and optional companion
resources.

### 3. Implement opt-in Egress

- Add a separate `AddEgress`-style resource using the official Egress image,
  defaulting an omitted version to `latest` while preserving explicit tags.
- Configure the LiveKit server URL, API credentials, and shared Redis
  connection.
- Expose typed settings for verified health/metrics ports and supported
  resource limits.
- Support documented recording/streaming output configuration, including a
  local-directory option only when an appropriate host/container volume mount
  is configured.
- Supply cloud/object-storage credentials through secret-aware parameters;
  never log them or imply that Egress is ready without a valid output target.
- Preserve supported container customization and an advanced configuration
  escape hatch with clear precedence and validation.

**Exit check:** Egress starts only when requested, uses the same server and
Redis configuration, and its output destination and required mount/credentials
are explicit and testable.

### 4. Implement opt-in SIP

- Add a separate `AddSip`-style resource using the official SIP image,
  independently versioned from the server and Egress.
- Configure its LiveKit server URL, API credentials, and shared Redis
  connection.
- Expose typed SIP/RTP listener configuration, verified transport protocols,
  optional health/metrics ports, and supported networking/container options.
- Allow provider/trunk settings and credentials to be supplied securely without
  automatically provisioning provider accounts, trunks, or dispatch rules.
- Document public reachability and suitable UDP/TCP routing for configured
  SIP/RTP ports, including limitations of local-only container networking.
- Preserve supported container customization and an advanced configuration
  escape hatch with clear precedence and validation.

**Exit check:** SIP starts only when requested, has explicit listener and
provider configuration, and documentation does not overstate local network
connectivity.

### 5. Confirm .NET wrapper compatibility

- Run the existing wrapper tests and examples against the shared contract to
  ensure the new host resources continue to provide compatible server
  configuration.
- Check the upstream SDK's public API for Egress/SIP operations and determine
  whether existing client registration already exposes them.
- Add wrapper surface only for a demonstrated upstream configuration/DI gap;
  do not create separate Egress/SIP clients or duplicate upstream APIs without
  that need.
- If changes are needed, add focused registration/options tests while keeping
  the wrapper independent of Aspire.

**Exit check:** consuming projects continue to configure the upstream SDK
through the same validated server URL/key/secret contract, with any necessary
extension covered by tests.

### 6. Verify and document

- Add tests for default and explicit Egress/SIP tags, opt-in behavior, shared
  Redis/server configuration, Egress output and volume settings, secret
  handling, health/metrics settings, SIP/RTP ports/protocols, and raw
  configuration precedence.
- Add an AppHost example enabling either service independently and another
  example showing shared Redis with both enabled.
- Document Egress local-volume and cloud-storage configuration, SIP provider
  responsibility, exposed ports, public-network requirements, and mutable
  `latest` tags.
- Run focused tests, build, and package validation for the hosting and wrapper
  packages.

## Acceptance criteria

1. Egress and SIP are independently opt-in resources with distinct official
   images and independently selectable tags.
2. Each enabled service uses the same LiveKit server credentials/URL and Redis
   connection as the core server.
3. Egress output configuration explicitly handles local volume mounts or
   cloud-storage credentials and never claims readiness without a configured
   destination.
4. SIP listener configuration clearly represents the verified SIP/RTP
   protocols and ports, and provider credentials remain secret.
5. Enabling one companion service does not start the other or change the
   server-only behavior.
6. Existing .NET SDK wrapper configuration remains compatible; any extension
   is justified by upstream public APIs and covered by tests.
7. Examples and tests document and verify service settings, external
   reachability limits, and secret-safe behavior.

## Risks and guardrails

- Egress may require runtime capabilities, browser/worker dependencies, or
  storage settings beyond simple environment variables; verify rather than
  promising a one-container setup without qualification.
- A bind-mounted output path must be valid in the Egress container, not merely
  on the AppHost machine.
- SIP and RTP often require public UDP/TCP routing and firewall configuration;
  port exposure in Aspire is not equivalent to public telephony reachability.
- Companion services must use the exact same Redis instance as the server;
  never silently create a disconnected Redis dependency.
- Keep the .NET wrapper scoped to upstream SDK setup, not container lifecycle
  or third-party provider provisioning.
