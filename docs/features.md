# Feature Catalog

Everything the platform does, grouped by area. Each entry notes the pull request(s) that introduced
it. The platform is a repo-agnostic CI/CD system: Jenkins builds any Git repo, artifacts land in
Sonatype Nexus, and a deployment service promotes them to Google Cloud Run or deploys whole .NET Aspire
apps to Kubernetes — all orchestrated locally by .NET Aspire.

See [architecture.md](architecture.md) for how these fit together,
[deployment/deploy-safety-features.md](deployment/deploy-safety-features.md) and
[deployment/preview-environments.md](deployment/preview-environments.md) for the deploy-path features in
depth.

---

## 1. Platform & orchestration

| Feature | What it does | PR |
| --- | --- | --- |
| Admin UI + two microservices | Blazor admin UI, the Jenkins CI service, and the deployment subsystem as separate services | #1 |
| .NET Aspire host | One AppHost orchestrates the services + SQL Server + RabbitMQ locally | #3, #5 |
| One-command bring-up | docker-compose for the whole stack | #2 |

## 2. CI service — Jenkins orchestration

| Feature | What it does | PR |
| --- | --- | --- |
| Editable orchestrator pipelines | Define/edit persisted pipeline chains that drive Jenkins jobs | #6 |
| Job DSL seed | Auto-seed the `cicd-*` Jenkins pipeline jobs | #7 |
| Repository management | Register/edit repos; activate/deactivate | #8 |
| Event bus + server-side runs | RabbitMQ/Wolverine with SQL outbox; live run status over SignalR | #11 |
| Cancel pipeline runs | Cancellation + `PipelineCancelled` event | #12 |
| Trim built-in pipeline | Chain ends at the Nexus publishes | #4 |

## 3. Build pipeline & supply-chain security

| Feature | What it does | PR |
| --- | --- | --- |
| Opt-in containerization | Only apps that opt in produce containers; copy-not-compile into a slim non-root runtime image | #19 |
| Repo-agnostic publish jobs | Build/scan/publish any Git repo, commit-pinned | #20 |
| Dedicated scan job | `cicd-scan`: CycloneDX SBOM, NuGet CVE gate, Trivy image scan | #21 |
| Robust Nexus reconcile | Searches `docker-private`; surfaces failures observably | #15 |

## 4. Artifact publishing

| Feature | What it does | PR |
| --- | --- | --- |
| Publisher microservice | Promote containers Nexus → remote registry, with delete + admin UI | #13, #14 |

## 5. Deployment service — Cloud Run

| Feature | What it does | PR |
| --- | --- | --- |
| Nexus → GAR → Cloud Run | Services × Environments × Mappings, typed `GarPush`/`CloudRunDeploy` steps, container inventory, event-driven auto-deploy | #18 |
| Cloud Run bootstrap | Create-on-missing service provisioning | #9 |

## 6. Deployment service — Aspire → Kubernetes (foundation)

| Feature | What it does | PR |
| --- | --- | --- |
| Aspire CI build/publish + handoff | Build a .NET Aspire app in CI, publish its Kustomize-output archive to Nexus, hand off to the deployment service | #24 |
| Explicit `SourceKey` handoff | Match CI publishes to registered apps by an explicit key, not just name | #25 |
| Edit apps + guard env deletion | Manage Aspire apps; prevent deleting an in-use environment | #35 |
| Repo-aware orchestrator defaults | Pipeline dropdown defaults to match the selected repo (e.g. "Aspire build") | #36 |
| Auto-provisioned pull secret | Provision the Nexus image-pull secret so aspirate-deployed pods can pull auth-required images | #37 |
| Push/publish stabilization | SDK registry recognition, immutable tagging, host resolution, `aspirate.json`/state leak guards, tar.gz packaging, handoff logging | #26–#34, #39 |

## 7. Aspire deploy-safety features

Operational features layered on the Aspire-app **run lifecycle**. Detailed in
[deploy-safety-features.md](deployment/deploy-safety-features.md).

| Feature | What it does | PR |
| --- | --- | --- |
| Deploy notifications | Slack + email on every deploy outcome (success/failure); per-channel, with a failures-only mute | #44 |
| Rollback | Redeploy a previous succeeded run's digest-pinned manifest (identical images, not a rebuild) | #45 |
| Promotion | Deploy an app's current manifest to another environment, running the same pinned artifacts | #46 |
| Approval gate | Runs targeting a *protected* environment park as `AwaitingApproval` until approved/rejected | #47 |
| Live-status & drift | On-demand cluster health + undeployed-changes + image drift (live vs. the run's `@sha256` snapshot) | #48 |
| Preview environments | Ephemeral per-PR/branch deploys into isolated `{app}-preview-{key}` namespaces, with TTL + teardown | #49 |

## 8. CI → preview handoff & correctness

| Feature | What it does | PR |
| --- | --- | --- |
| CI PR/branch preview handoff | Publishes carry the build branch; a per-app `MainBranch` routes main → deploy, other branches → create/refresh a preview | #51 |
| Teardown webhook | `POST /previews/webhook` tears a preview down on PR close/merge; TTL sweeper is the fallback | #51 |
| Namespace pinning fix | Deploys now land in the requested namespace (kustomization `namespace:` + ensure-exists), giving previews true isolation and honoring each environment's namespace | #50 |

## 9. SCA / SBOM visibility

| Feature | What it does | PR |
| --- | --- | --- |
| Aspire per-image SBOM view | Surface SBOMs/vulnerabilities for Aspire app images; fix build-detail SBOM links | #40 |
| Aspire SBOM dependency graph | Visualize an Aspire app's per-image SBOMs | #41 |

## 10. Packaging & tooling

| Feature | What it does | PR |
| --- | --- | --- |
| Deployment.Api Dockerfile | The full stack builds as images | #42 |
| aspirate + kubectl in the image | Container-side Aspire deploys | #43 |

## 11. Progressive delivery (rollout strategies)

Deploy safely on **vanilla Kubernetes** — no service mesh or Argo Rollouts required. See
[blue-green-demo.md](demos/blue-green-demo.md).

| Feature | What it does | PR |
| --- | --- | --- |
| Blue-green + canary (per-service K8s) | Deploy the new version to a parallel slot, health-gate it, then cut over by swapping the Service selector; automatic or manual promotion, auto-rollback if the slot never goes healthy. Canary shifts a replica share first | #53 |
| Namespace-isolated blue-green (whole Aspire app) | Deploy the new version into a parallel `{ns}-{slot}` namespace, health-gate the whole app, then promote (make green active, retire the old namespace); automatic/manual per app, auto-rollback deletes green | #56 |

## 12. Deploy observability

| Feature | What it does | PR |
| --- | --- | --- |
| OTLP deploy metrics + tracing | Custom deploy metrics + spans exported over OTLP (visible in the Aspire dashboard) | #55 |
| DORA dashboard | Deployment frequency, lead time, change-failure rate, and MTTR from run history | #55 |

## 13. Browsable preview URLs (ingress)

| Feature | What it does | PR |
| --- | --- | --- |
| ingress-nginx URLs for previews | A preview's frontend gets an auto-stamped Ingress at `{key}.preview.localtest.me` (a public wildcard → `127.0.0.1`), so it's browsable with no port-forward; surfaced as a link on the Previews page | #56 |

## 14. Kubernetes admin screens (web-admin)

A **Kubernetes** section in web-admin — see [k8s-admin-demo.md](demos/k8s-admin-demo.md).

| Feature | What it does | PR |
| --- | --- | --- |
| Cluster browser | Read-only: pick a context, list every namespace, drill into workloads / pods / services / ingresses, tail pod logs | #57 |
| Deployed-apps overview | Consolidated live health of all Aspire apps + active previews (with URLs), aggregated from existing endpoints | #57 |
| Lifecycle actions | Rolling-restart a Deployment, scale replicas, delete a pod — each behind a confirm | #58 |

---

*Docs added along the way: architecture-diagram rewrite (#22), `kind`-to-Nexus setup script (#38), the
deploy-safety / preview / README / feature-catalog docs, and the demo runbooks under [demos/](demos/).*
