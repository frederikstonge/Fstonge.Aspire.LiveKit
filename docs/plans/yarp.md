# YARP Reverse Proxy Plan

## Goal and scope

Add an optional YARP resource to the Aspire application using Aspire's
`Aspire.Hosting.Yarp` integration. Use it as the public HTTP/WebSocket edge
for LiveKit signaling and, if needed, the example API. Keep it independently
deployable and autoscalable.

YARP handles HTTP and WebSocket traffic. It does not proxy LiveKit's UDP RTC
media traffic or replace the TCP/UDP services needed by LiveKit, TURN, SIP, or
RTP. Those paths must remain directly reachable through suitable Kubernetes
Services and cloud networking.

## 1. Confirm routing and ownership

- Decide which routes YARP owns: LiveKit HTTP/WebSocket signaling, the example
  API, or both. Avoid exposing management endpoints unless explicitly
  required.
- Confirm LiveKit's externally advertised URL and WebSocket behavior, TLS
  termination, path handling, and any required forwarded headers.
- Determine whether YARP is a public edge behind a cloud load balancer or an
  internal proxy behind another ingress controller. Avoid unintentionally
  creating two competing public ingress layers.
- Keep media, TURN, SIP, RTMP, WHIP, and RTP routing requirements separate
  from YARP's HTTP routes.

## 2. Model and configure YARP through Aspire

- Add the `Aspire.Hosting.Yarp` package to the AppHost, aligned with the
  repository's Aspire package version (`13.6.4` at plan creation). Confirm
  compatibility before implementation and keep the package version pinned.
- Model YARP directly with `AddYarp("gateway")`; do not create a separate
  proxy project unless a requirement cannot be met by the hosting integration.
- Configure routes and clusters through `WithConfiguration`. Use AppHost
  resource or endpoint references as upstream destinations so Aspire can
  provide the correct addresses in local runs and deployed environments.
- Define explicit route matching, path/header transforms, and upstream
  behavior. Keep routes and environment-specific hostnames controlled by the
  AppHost configuration rather than hard-coded local addresses.
- Add health checks, structured logs, tracing, and metrics using the supported
  integration and Aspire resource configuration. Do not log credentials,
  authorization headers, or sensitive query values.
- Apply explicit route allowlists, request-size and timeout limits, and
  forwarded-header trust rules. Do not blindly trust client-supplied
  `X-Forwarded-*` headers.

## 3. Publish YARP with Aspire's Kubernetes integration

- Publish the YARP resource created by `AddYarp` with the
  `Aspire.Hosting.Kubernetes` integration used by the deployment plan.
- Configure its Kubernetes Service for the required HTTP/HTTPS port. Terminate
  TLS at the chosen edge (YARP or cloud load balancer) and document the
  certificate and forwarded-protocol arrangement.
- Route public HTTP and WebSocket requests to YARP using the selected
  load-balancer or ingress integration. Confirm that WebSocket upgrade
  requests and long-lived connections are supported end to end.
- Keep LiveKit media ports and any companion-service ports in their own
  Kubernetes Services and public network configuration; do not route them
  through YARP.
- Use environment-specific configuration and secret references for TLS
  material and any upstream credentials. Do not commit secret values or bake
  them into the proxy image.

## 4. Scale and operate independently

- Begin with an independently configurable replica count and resource
  requests/limits for YARP.
- Add an HPA using CPU or memory as the initial signal after setting resource
  requests. Tune minimum/maximum replicas, stabilization, and scale-down
  behavior using representative WebSocket and HTTP load tests.
- Ensure readiness reflects the proxy's ability to serve traffic and liveness
  detects a stuck process without making transient upstream outages restart
  every proxy replica.
- Monitor request rate, latency, failures, active connections, upstream
  health, and scaling limits. Define alerts and operational ownership.

## 5. Verify behavior and security

- Test HTTP routes, WebSocket upgrade and reconnect behavior, TLS, path and
  header forwarding, health endpoints, and upstream failures.
- Verify API authentication and authorization boundaries, route allowlists,
  trusted proxy settings, timeouts, and that sensitive values are not logged.
- Deploy to a Kubernetes test cluster and confirm that scaling YARP does not
  change LiveKit's direct UDP/TCP media connectivity.
- Load-test sustained WebSocket connections as well as short HTTP requests;
  verify rollout and scale-down behavior with active connections.
- Document public URLs, exposed ports, TLS ownership, required firewall
  rules, configuration, deployment, and rollback.

## Completion criteria

YARP is an optional, observable, independently scalable HTTP/WebSocket edge
resource in the Aspire AppHost and generated Kubernetes deployment. LiveKit
signaling works through the proxy, while real-time media and other non-HTTP
traffic use separately documented Kubernetes networking.
