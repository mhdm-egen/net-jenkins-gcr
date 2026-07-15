# Demo: Safe Delivery with Blue-Green Aspire Deploys

A ~8-minute live demo that lands one message: **this platform makes shipping *safe* by default.**
The star is namespace-isolated blue-green with **auto-rollback** — deploy a whole .NET Aspire app
to a shadow namespace, health-gate it, cut over only when it's healthy, and delete it if it isn't —
all on vanilla Kubernetes, no service mesh.

> Runs against the whole-Aspire-app deploy path. Feature reference:
> [../deployment/deploy-safety-features.md](../deployment/deploy-safety-features.md).

## Live URLs

Ports are assigned by the Aspire host per run — read them from the dashboard or process, but a
typical local session looks like:

| Surface | URL | Use in the demo |
| --- | --- | --- |
| web-admin | `http://localhost:7232` | Drive everything from **Deployment → Aspire apps** |
| deployment-api | `http://localhost:7230` | `curl` levers (optional) |
| Aspire dashboard | `https://localhost:2469` | Traces/logs B-roll |

The demo app targets the **`bg-test`** environment (`kubectl` context `docker-desktop`, base
namespace `bgtest`). Blue-green slots land in `bgtest-blue` / `bgtest-green`.

## Set the stage (before the audience arrives)

Arrange three panes so the room sees cause → effect at once:

1. **Browser** → web-admin, `Aspire apps` page.
2. **Terminal A — the cluster, live:**
   ```bash
   kubectl get ns -w | grep --line-buffered bgtest
   # second tab:
   watch -n2 'kubectl get deploy,po -n bgtest-green 2>/dev/null; echo ---; kubectl get deploy,po -n bgtest-blue 2>/dev/null'
   ```
3. **Terminal B — uptime witness:** a loop hitting the *live* app so the room watches uptime while
   you deploy and break things. (Port-forward the active slot's frontend, or curl the service.)

Pre-stage **two manifest URLs** for the same app so mid-demo you only flip one field:

- **good:** `http://nexus:8081/repository/raw-hosted/sampleapp/1.0.0-ci.29.ge0a1643/aspirate-output.tar.gz`
- **bad:** the same archive with a bogus image tag, served locally:
  ```bash
  # build a chaos archive: rewrite both image tags to a tag that can't be pulled
  d=$(mktemp -d); curl -s -o "$d/g.tgz" "http://nexus:8081/repository/raw-hosted/sampleapp/1.0.0-ci.29.ge0a1643/aspirate-output.tar.gz"
  mkdir "$d/x"; tar xzf "$d/g.tgz" -C "$d/x"
  sed -i 's#:29-e0a1643#:doesnotexist-999#' "$d/x/aspirate-output/apiservice/deployment.yaml" "$d/x/aspirate-output/webfrontend/deployment.yaml"
  ( cd "$d/x" && tar czf ../served-bad.tar.gz aspirate-output )
  ( cd "$d" && python -m http.server 7788 --bind 127.0.0.1 )   # leave running
  # bad URL → http://localhost:7788/served-bad.tar.gz
  ```
  > Port 7788 avoids Windows' excluded ranges; 8899 and other low ports are reserved.

Register one app in the UI: **BgDemo** → environment **bg-test**, **Rollout strategy = Blue-green**,
**Promotion mode = Automatic**, manifest = the **good** URL.

## The narrative (~8 min)

### Act 0 — the pitch (30s)
> "Vanilla Kubernetes. No service mesh, no Argo Rollouts. We still give whole .NET Aspire apps
> blue-green safety — deploy to a shadow namespace, health-gate it, then cut over. If it's sick,
> it never sees traffic."

### Act 1 — normal deploy / bootstrap (1 min)
Click **Deploy**. In Terminal A a `bgtest-blue` namespace appears, pods go Ready, the run →
**Succeeded**, `activeSlot=blue`. Open the app's **live status / drift** view (heart-monitor icon):
green health, real workloads. *"That's the baseline running."*

### Act 2 — blue-green auto-promote (1.5 min)
Bump the version and **Deploy** again. Narrate the split screen: a *parallel* `bgtest-green`
namespace spins up **while blue keeps serving** — Terminal B never drops a request. Green passes the
health gate → the platform retires `bgtest-blue` and flips `activeSlot=green`.
> "Zero-downtime cutover — and the old version stuck around until the new one proved itself."

### Act 3 — 🎯 the money shot: break it (2 min)
Edit the app's manifest source to the **bad** URL and **Deploy**. This is the beat that sells it:

- Terminal A: `bgtest-green` pods go **`ImagePullBackOff`** and stay there.
- The health gate refuses to pass — narrate the ~2-minute countdown: *"it's giving the new version
  every chance…"*
- Gate expires → green namespace **deleted**, run → **Failed** with a plain-English reason
  (*"green namespace 'bgtest-green' did not become healthy — rolled back"*).
- **The whole time, Terminal B never blinks.** The broken build got exactly zero user traffic and
  cleaned itself up.

> "A bad build in a normal pipeline is an outage. Here it's a non-event."

### Act 4 — human-in-the-loop for prod (1.5 min)
Switch **Promotion mode = Manual**, point back at the **good** manifest, and **Deploy**. It
health-gates, then **parks** in `AwaitingPromotion` — green is healthy and waiting. Show the
**Promote / Roll back** buttons on the run row. **Promote** → cutover + old namespace retired.
> "Same safety, but a human makes the call for protected environments."

Tie-in: the **approval gate** does the same for the deploy *itself* on protected environments.

### Encore (pick one, ~1 min each)
- **Preview environments** — open a PR; watch an ephemeral per-PR namespace appear, and tear down on close.
- **Full CI→CD handoff** — kick a Jenkins build; it publishes to Nexus and auto-triggers the deploy, end to end.
- **Aspire dashboard** — flip to traces/logs for the deploy you just ran as proof it's all observable.

## Closing line
> "Direct, blue-green, or manual-gated — per app, one dropdown. No mesh, no new infra.
> Safe delivery is the default, not a project."

## The levers (cheat sheet)

Everything is clickable in web-admin; these are the equivalent API calls if you'd rather script it.
`APP` = the app id, `RUN` = a run id, `API` = the deployment-api base URL.

| Beat | UI | API |
| --- | --- | --- |
| Deploy | **Deploy** button | `POST $API/api/deployment/aspire-apps/$APP/deploy` |
| Flip strategy/mode/manifest | **Edit** dialog | `PUT $API/api/deployment/aspire-apps/$APP` (body carries `strategy`, `promotionMode`, `manifestSource`) |
| Promote a parked run | **Promote** (move-up) icon | `POST $API/api/deployment/aspire-runs/$RUN/promote` |
| Roll back a parked run | **Roll back** (undo) icon | `POST $API/api/deployment/aspire-runs/$RUN/rollback` |
| Watch a run | Recent deployments table | `GET $API/api/deployment/aspire-runs/$RUN` |
| Live status / drift | heart-monitor icon | `GET $API/api/deployment/aspire-apps/$APP/status` |

## Reset between runs
```bash
# delete the demo app (UI: trash icon) and both slots
curl -s -X DELETE "$API/api/deployment/aspire-apps/$APP"
kubectl delete ns bgtest-blue bgtest-green --ignore-not-found=true
# stop the chaos server when done
# (kill the python -m http.server 7788 process)
```

## Talking points / honest caveats
- **Cutover is a namespace / active-slot flip, not a live-traffic split.** With no ingress/mesh there's
  no percentage weighting — the new slot becomes *the* active slot atomically once it's healthy. That's
  the honest trade for "works on plain Kubernetes."
- The health gate polls the target namespace's overall health for up to ~2 minutes before giving up.
- App-level **Rollback** / **Promote-to-environment** still deploy `Direct` (they bypass the gate).
- Old apps default to `Direct` = the previous in-place behavior; blue-green is strictly opt-in per app.
