# Demo: Kubernetes Admin Screens

A ~5-minute tour of the **Kubernetes** section in web-admin — browse the local cluster, read pod logs,
and run lifecycle actions (restart / scale / delete pod) without dropping to `kubectl`. Read-only
browsing plus a small set of mutating actions, all over the deployment service's in-process kube client.

Pairs with [blue-green-demo.md](blue-green-demo.md) and [build-pipeline-demo.md](build-pipeline-demo.md):
those *produce* deploys; this one *operates* the cluster they land on.

## Live URLs

| Surface | URL | Use in the demo |
| --- | --- | --- |
| web-admin | `http://localhost:7232` | Drive from **Kubernetes → Cluster / Deployed apps** |
| deployment-api | assigned per run (Aspire dashboard) | `curl` levers (optional) |

> The screens read the cluster via `IKubeClientFactory` (kubeconfig + context) — the same seam the
> deploy paths use. Requires a reachable cluster; docker-desktop's `docker-desktop` context is the default.

## Set the stage

Have something deployed to look at — either an existing app namespace (`sampleapp`) or spin up a
preview first (see [build-pipeline-demo.md](build-pipeline-demo.md)) so there's a live frontend +
ingress to inspect. ingress-nginx installed makes the ingress rows meaningful.

## The narrative (~5 min)

### Act 0 — the pitch (15s)
> "See and operate the local cluster from the same console that deploys to it — namespaces, workloads,
> pods, services, ingresses, logs, and the everyday lifecycle actions — no kubectl, no context-switch."

### Act 1 — browse the cluster (1.5 min)
Open **Kubernetes → Cluster**. Pick the **context** (`docker-desktop`). The namespace list shows every
namespace with its phase and age, and flags `*-preview-*` ones. Filter to a namespace and click it:

- **Workloads** — each Deployment with a health chip (Healthy/Degraded/Down), image, `ready/desired`, restarts.
- **Pods** — phase, restarts, ready.
- **Services** — type, clusterIP, ports.
- **Ingresses** — class, clickable **URLs**, and the `host/path → service:port` backends.

Point at `ingress-nginx` (the controller workload + its LoadBalancer/admission services) or a preview
namespace (webfrontend/apiservice + the `app-frontend` ingress with its browsable URL).

### Act 2 — read a pod's logs (45s)
On any pod row, click the **logs** icon. The dialog tails the pod's log (auto-resolving the container
for multi-container pods) with a Refresh. Great for "why is this pod Degraded?" — the ingress
controller's log even shows the access lines for your preview URLs.

### Act 3 — 🎯 lifecycle actions (1.5 min)
On a **safe** target (e.g. `sampleapp/webfrontend`):

- **Scale** — the Tune icon → set replicas → the workload's `ready/desired` climbs on the next refresh.
- **Restart** — the RestartAlt icon → confirm → a rolling restart (new ReplicaSet; pod hash changes).
- **Delete pod** — the Delete icon on a pod → confirm → the controller reschedules a replacement.

Each action confirms first and refreshes the namespace view after. Merge-patches only — nothing is
recreated wholesale.

### Act 4 — the one-screen overview (45s)
Open **Kubernetes → Deployed apps**: every registered Aspire app with a **live health** chip and a
sync/drift/pending badge, plus every active preview with its clickable **URL** — aggregated from the
existing endpoints. The at-a-glance "what's running and is it healthy" view.

### Closing line
> "Read the cluster, tail a log, restart or scale a workload — from the deploy console, in seconds."

## Levers (cheat sheet)

Everything is clickable in web-admin; the equivalent API (`API` = deployment-api base, `context`
optional query param):

| Action | UI | API |
| --- | --- | --- |
| List contexts | Context selector | `GET $API/api/deployment/k8s/contexts` |
| List namespaces | Namespace table | `GET $API/api/deployment/k8s/namespaces` |
| Namespace detail | Click a namespace | `GET $API/api/deployment/k8s/namespaces/{ns}` |
| Pod logs | Logs icon | `GET $API/api/deployment/k8s/namespaces/{ns}/pods/{pod}/logs?tail=500` |
| Restart deployment | RestartAlt icon | `POST $API/api/deployment/k8s/namespaces/{ns}/deployments/{name}/restart` |
| Scale deployment | Tune icon | `POST $API/api/deployment/k8s/namespaces/{ns}/deployments/{name}/scale` body `{"replicas":N}` |
| Delete pod | Delete icon | `DELETE $API/api/deployment/k8s/namespaces/{ns}/pods/{pod}` |

## Caveats
- **No auth** on these endpoints yet — fine for a local cluster, gate them before anything shared.
- Reads are **Deployments + Pods + Services + Ingresses**; no ConfigMaps/Secrets/StatefulSets/Jobs, no
  Node/Events views, and pod logs are a **tail snapshot** (not a live follow) — natural follow-ups.
- Lifecycle actions are **merge-patches** (restart annotation, scale replicas) or a pod delete; they
  don't edit specs or create resources.
