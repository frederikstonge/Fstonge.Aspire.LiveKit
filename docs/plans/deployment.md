# Aspire, Kubernetes, and Helm Deployment Plan

## 1. Use Aspire's Kubernetes integration as the deployment source

- Add the `Aspire.Hosting.Kubernetes` package to the example AppHost, aligned
  with the repository's Aspire 13.6.4 package versions. Confirm API and CLI
  compatibility before implementation, and keep the integration version
  explicitly pinned.
- Add a Kubernetes environment to the AppHost using
  `AddKubernetesEnvironment`. Keep it selectable by deployment environment so
  local Aspire development continues to use its existing container resources
  without requiring a cluster.
- Configure the environment's Helm settings with `WithHelm` only where needed:
  namespace, release name, chart name/version, and description. Helm is the
  integration's default deployment engine.
- Use `aspire publish` to generate deployment artifacts, including the Helm
  chart, for CI or a later deployment step. Use `aspire deploy` when Aspire
  should apply the deployment directly using the active `kubectl` context.
  Treat these as distinct workflows; `deploy` does not apply previously
  published artifacts.
- Define environment-specific configuration for image tags, public addresses,
  secrets, storage, and resource sizing. Preserve unresolved parameters in
  published output and resolve/provide them securely in the deployment
  workflow.
- Prefer generated artifacts over hand-editing generated manifests. If the
  Kubernetes integration cannot express a required LiveKit feature or the
  desired chart lifecycle, document that gap and decide whether to add custom
  Kubernetes resources or maintain a separate chart as a deliberate fallback.

See the [Aspire deployment model](https://aspire.dev/deployment/deploy-with-aspire/)
and [Kubernetes hosting integration](https://aspire.dev/integrations/compute/kubernetes/)
references.

## 2. Map the AppHost resource graph to Kubernetes and Helm

- Publish the API, Redis, LiveKit server, and any enabled companion services
  from the existing AppHost resource graph. Confirm each resource type is
  supported by the integration; configure container resources with
  `PublishAsKubernetesService` as needed.
- Add the optional YARP resource with Aspire's `Aspire.Hosting.Yarp`
  integration and `AddYarp`, then publish it through the Kubernetes
  integration as the public HTTP/WebSocket edge. See the [YARP plan](./yarp.md)
  for package configuration, routes, deployment, scaling, and validation
  details.
- Add a container registry resource and associate it with the Kubernetes
  environment so built application images are accessible to the cluster.
- For workloads that require durable storage, model claims with the
  integration's `AddPersistentVolume` API and bind them to workloads using the
  corresponding Kubernetes volume APIs. Supply storage class and capacity
  through deployment configuration. Treat Redis as either a chart-managed
  dependency or an externally managed service, and document the selected
  production topology.
- Use per-resource Kubernetes customization for ports, services, workload
  settings, and other supported options. Add custom Kubernetes manifests to
  the generated chart for resources that the integration does not model
  directly; evaluate this for HPAs, network policies, certificates, and
  provider-specific load-balancer annotations.
- Keep optional Ingress, Egress, and SIP services opt-in. Verify that generated
  Services can express their distinct protocols and port ranges before
  committing to the chart structure.
- Do not commit generated output as a hand-maintained source of truth unless
  there is a specific release or review requirement. Keep chart customization
  in the AppHost model or in a documented, reproducible post-publish step.
- Use Kubernetes Secrets or an external secret provider for LiveKit API
  credentials and storage credentials. Do not put secret values in Helm
  defaults, committed configuration, or generated artifacts intended for
  publication.
- Document image building, `aspire publish`/`aspire deploy`, chart packaging,
  installation, upgrades, and rollback.

## 3. Make LiveKit networking and distributed operation explicit

- Configure LiveKit replicas to share Redis and use the supported distributed
  deployment settings.
- Design public networking for the HTTP/WebSocket API, TCP RTC, and UDP RTC mux
  ports. Confirm that the chosen cloud load balancer and Kubernetes service
  setup handle both TCP and UDP correctly.
- If YARP is enabled, use it only for the HTTP/WebSocket edge, including LiveKit
  signaling where validated. Keep UDP RTC media and other non-HTTP traffic on
  separate direct Kubernetes Services and network paths; YARP does not replace
  them.
- Set the externally advertised LiveKit address and any TURN/TLS configuration
  through deployment values and secrets.
- Validate routing behavior with multiple LiveKit replicas; ordinary HTTP load
  balancing alone is not sufficient evidence that real-time media traffic is
  routed correctly.
- Treat SIP signaling and RTP port ranges, and Ingress RTMP/WHIP endpoints, as
  separate networking requirements. Document firewall and load-balancer
  requirements.

## 4. Implement autoscaling with safe bounds

- Add a Horizontal Pod Autoscaler for the API, using CPU or memory utilization
  as an initial signal.
- Check whether the Aspire Kubernetes integration provides a first-class
  scaling API for the pinned version. If not, add the HPA as a custom
  Kubernetes manifest to the generated chart (or use a reproducible chart
  customization step); do not assume setting a fixed replica count enables
  autoscaling.
- Add a separately configurable LiveKit scaling policy with minimum and
  maximum replicas. Start with resource-based scaling only if load tests show
  that it correlates with LiveKit capacity; otherwise use suitable custom
  metrics such as active participants, rooms, or node load.
- Keep Egress and Ingress scaling independent of the LiveKit server. Select
  metrics appropriate to their work, and ensure job or stream processing
  remains safe when replicas change.
- Treat cluster autoscaling as a separate infrastructure prerequisite; pod
  autoscalers cannot create node capacity themselves.
- Configure scale-down behavior to avoid disrupting active sessions or
  ongoing Egress jobs, and document operational limits.

## 5. Add health, observability, and rollout safeguards

- Configure readiness and liveness probes based on the services' supported
  health endpoints.
- Set resource requests and limits, graceful termination behavior, and a
  rollout strategy appropriate for long-lived real-time connections.
- Export logs and metrics, and add alerts for failed probes, pending pods,
  scaling limits, Redis availability, and service capacity.
- Make replica counts, autoscaling bounds, and metric targets adjustable per
  environment.

## 6. Verify and document

- Validate the chart with linting and rendered-template checks.
- Run `aspire publish` in CI and validate the generated chart and Kubernetes
  manifests. Test `aspire deploy` separately against a disposable cluster if
  direct deployment is a supported workflow.
- Deploy to a Kubernetes test cluster and verify installation, upgrades,
  rollback, secrets, probes, and optional-service toggles.
- If YARP is enabled, verify WebSocket upgrades and sustained connections
  through the public edge, while independently testing direct LiveKit media
  connectivity.
- Load-test with multiple LiveKit replicas, checking UDP/TCP connectivity,
  participant joins, room distribution, and scale-up/scale-down behavior.
- Update the README and deployment documentation with prerequisites, Helm
  commands, required network rules, scaling caveats, and the difference
  between local Aspire behavior and production Kubernetes behavior.

## Completion criteria

A documented Helm deployment can be installed and upgraded from the
AppHost-defined topology using Aspire's Kubernetes hosting integration;
published chart artifacts are reproducible; secrets remain outside the chart;
public real-time traffic works across replicas; and autoscaling is bounded,
observable, and validated under load.
