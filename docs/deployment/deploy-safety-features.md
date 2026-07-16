# Aspire Deploy-Safety Features

Operational features layered on the **Aspire → Kubernetes** deploy path (deploying a whole
.NET Aspire app to a cluster via Aspir8). Each is a thin, event-driven slice over the
`AspireApplication` / `AspireApplicationRun` aggregates in the deployment service.

> This page covers the *whole-Aspire-app* deploy path. The per-service Cloud Run model is
> documented in [deployment-model-decisions.md](deployment-model-decisions.md).

All endpoints are under the deployment-api (`deployment-api` resource in the Aspire dashboard);
paths below are relative to it. The UI lives in **web-admin → Deployment → Aspire apps / Previews**.

## Feature map

| Feature | What it gives you | Endpoint(s) | UI |
| --- | --- | --- | --- |
| Deploy notifications | Slack + email on every deploy outcome | — (config only) | — |
| Rollback | Redeploy a previous succeeded run's pinned manifest | `POST /aspire-apps/{id}/rollback` | Restore icon on a succeeded run |
| Promotion | Deploy an app's current manifest to another environment | `POST /aspire-apps/{id}/promote` | Promote (move-up) icon |
| Approval gate | Park deploys to protected environments until approved | `POST /aspire-runs/{id}/approve` · `/reject` | Approve / Reject on awaiting runs |
| Blue-green rollout | Deploy to a parallel namespace, health-gate, then promote or roll back | `POST /aspire-runs/{id}/promote` · `/rollback` | Strategy on the app; Promote / Rollback on awaiting-promotion runs |
| Live-status & drift | Cluster health + config/image drift for an app | `GET /aspire-apps/{id}/status` | Status (heart-monitor) icon |
| Preview environments | Ephemeral per-PR/branch deploys (browsable ingress URL) | `…/previews*` | Previews page — see [preview-environments.md](preview-environments.md) |

The deploy itself (`POST /aspire-apps/{id}/deploy`) creates an `AspireApplicationRun`; the run raises
`AspireApplicationRunRequested`, and a Wolverine handler shells out to `aspirate apply`. Every feature
below hooks that same run lifecycle.

---

## Deploy notifications

Every Aspire deploy that reaches a terminal state (Succeeded / Failed) fans out a notification. Off by
default — enable per channel under `Deployment:Notifications`.

```jsonc
// deployment-api configuration (Deployment:Notifications)
{
  "Deployment": {
    "Notifications": {
      "OnlyFailures": false,            // true = suppress success notifications
      "Slack":  { "Enabled": true, "WebhookUrl": "https://hooks.slack.com/services/…" },
      "Email":  {
        "Enabled": true,
        "SmtpHost": "smtp.example.com", "Port": 587, "UseSsl": true,
        "Username": "…", "Password": "…",   // set via user-secrets / env, never in git
        "From": "cicd@example.com",
        "To":   [ "team@example.com" ]
      }
    }
  }
}
```

- **Both channels** send independently; either can be enabled alone.
- `OnlyFailures: true` mutes the success path — useful for a noisy channel.
- Secrets (`Slack:WebhookUrl`, `Email:Password`) belong in user-secrets or environment variables, per the
  project's security rule (never commit secrets).

---

## Rollback

Roll an app back to the exact artifacts a previous **succeeded** run deployed. Because deploys are
digest-pinned, a rollback re-applies the identical images — not a rebuild.

```http
POST /api/deployment/aspire-apps/{id}/rollback
{ "targetRunId": "…", "triggeredBy": "ui" }
```

- Creates a new run that redeploys the target run's `ManifestSource` / `Version`.
- The app record is repointed to the rolled-back manifest (`AspireApplication.RollbackTo`), so the app —
  and any later auto/manual deploy — reflects what's now running.
- **UI:** the deployments table shows a **Restore** icon on each `Succeeded` run.

---

## Promotion

Deploy an app's **current** manifest to a *different* Kubernetes environment (e.g. `staging` → `prod`)
without a rebuild. The images are digest-pinned, so promotion runs the exact same artifacts in the new
environment's context/namespace.

```http
POST /api/deployment/aspire-apps/{id}/promote
{ "targetEnvironmentId": "…", "triggeredBy": "ui" }
```

- Creates a run targeting the chosen environment's kube context + namespace.
- If the target environment is **protected**, the promotion parks for approval (below).
- **UI:** the **Promote** dialog on each app lists the available Kubernetes environments.

---

## Approval gate for protected environments

Mark an environment **Protected** and *any* run that targets it — manual deploy, promotion, rollback, or
the CI auto-deploy handoff — parks as **AwaitingApproval** instead of applying.

```http
POST /api/deployment/aspire-runs/{id}/approve   { "approvedBy": "ui" }
POST /api/deployment/aspire-runs/{id}/reject    { "rejectedBy": "ui", "reason": "…" }
```

Mechanism:

- A run for a protected env is constructed in `AwaitingApproval` and **does not** raise
  `AspireApplicationRunRequested`, so the executor never picks it up.
- **Approve** → status `Pending`, records `DecisionBy`, re-raises the request → the executor deploys the
  same pinned artifacts.
- **Reject** → status `Rejected`, records who/why, raises **no** event (so no spurious failure
  notification fires).
- `409` if the run isn't awaiting approval.

**UI:** toggle **Protected** on an Environment; awaiting runs show **Approve** / **Reject** buttons and an
"Approval" badge. Rejected runs are styled as errors with the reason on hover.

---

## Blue-green rollout (namespace-isolated)

Set an app's **Strategy = BlueGreen** and each deploy goes to a *parallel* `{namespace}-{slot}`
namespace (blue/green), where the whole app is **health-gated** before it takes over — no service mesh
required. Cutover is a namespace / active-slot flip (an honest limitation: not a live-traffic split).

```http
POST /api/deployment/aspire-runs/{id}/promote    { "promotedBy": "ui" }
POST /api/deployment/aspire-runs/{id}/rollback   { "rolledBackBy": "ui", "reason": "…" }
```

Flow (per app, configurable **PromotionMode**):

- **Bootstrap** (first deploy) → lands in the initial slot and becomes active immediately.
- **Automatic** → deploy green, health-gate it; healthy → delete the old namespace + flip the active
  slot (run **Succeeds**); never healthy within the gate deadline → delete green (auto-rollback, run **Fails**).
- **Manual** → deploy green, health-gate it; healthy → park the run as **AwaitingPromotion**. **Promote**
  flips the active slot and retires the old namespace; **Rollback** deletes green and leaves the live
  deploy untouched (run **RolledBack**).

The gate + settle run on a non-cancellable path so an interrupted deploy always rolls back cleanly
rather than leaking the green namespace. Walkthrough: [blue-green-demo.md](../demos/blue-green-demo.md).

> The **per-service** Cloud Run/K8s path has its own blue-green **and canary** (real Service-selector
> cutover / replica-weighted canary) — see [deployment-model-decisions.md](deployment-model-decisions.md).

---

## Live-status & drift

An on-demand read of the app's real cluster state, plus two drift signals.

```http
GET /api/deployment/aspire-apps/{id}/status
```

Returns:

| Field | Meaning |
| --- | --- |
| `overallHealth` | Worst-of the workloads: `Healthy` / `Degraded` / `Down` / `Unknown` |
| `reachable` + `error` | An unreachable cluster / missing namespace is **data**, not an exception |
| `hasUndeployedChanges` | The app's current manifest/version differs from what the last successful run deployed (e.g. a CI publish with auto-deploy off) |
| `hasImageDrift` | A live workload runs a **different image** than the app last deployed — the cluster changed out of band |
| `workloads[]` | Per Deployment: running image, `ready/desired` replicas, health, per-pod restarts, and a `drifted` flag + `expectedImage` |

How the drift baseline works:

- On a **successful** deploy, the executor snapshots the applied workloads' images
  (`AspireApplicationRun.DeployedImages`). Since deploys digest-pin, this captures immutable
  `@sha256` refs.
- The status query diffs the live cluster's images against that snapshot → `hasImageDrift` +
  per-workload `drifted`/`expectedImage`.

**UI:** the **Status** icon on each app opens a dialog with the health chip, an undeployed-changes banner, an
image-drift banner, and a per-workload table (running image, ready/desired, restarts, drift chip).

---

## Namespace pinning (applies to every deploy)

`aspirate apply` would otherwise apply the generated manifests to whatever namespace they bake in
(usually `default`). The runner pins the **requested** namespace before applying:

- Writes a top-level `namespace:` into the root `kustomization.yaml` so Kustomize rewrites every resource
  into the environment's namespace.
- Server-side-applies that namespace first so the resources land cleanly.

This is why each environment's configured namespace is honored, and why [preview
environments](preview-environments.md) get true per-PR isolation.

---

## Configuration surface (deployment-api)

| Section | Key | Default | Purpose |
| --- | --- | --- | --- |
| `Deployment:Aspirate` | `Executable` | `aspirate` | aspirate CLI (name on PATH or full path) |
| | `Kubeconfig` | *(empty)* | kubeconfig for non-default clusters |
| | `PullRegistry` | *(empty)* | registry the **cluster** pulls from (e.g. `host.docker.internal:8082`) |
| | `EnsurePullSecret` | `false` | provision a Nexus dockerconfig pull secret in the target namespace |
| | `PullSecretName` | `nexus-pull` | name of that secret |
| | `ApplyTimeoutSeconds` | `300` | max `aspirate apply` duration |
| `Deployment:Previews` | `SweepIntervalMinutes` | `15` | how often the TTL sweeper reaps expired previews |
| `Deployment:Notifications` | *(see above)* | off | Slack + email deploy notifications |
| `Deployment:Nexus` | `RegistryV2Url` | *(empty)* | Nexus docker-v2 endpoint for digest pinning |

Most of these are wired from the AppHost's parameters/user-secrets — see the [root README](../../README.md)
and [aspire-k8s-local-runbook.md](aspire-k8s-local-runbook.md).
