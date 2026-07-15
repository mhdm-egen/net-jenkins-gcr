# Demo: Commit → Scanned Artifacts → Auto-Deploy (CI Build Pipeline)

A ~6-minute live demo of the **CI / build** side: one commit becomes a container image per
Aspire resource, gets vulnerability-scanned, publishes immutable artifacts to Nexus, and **hands
itself off to a deployment** — with a security gate that can stop a bad build cold.

Pairs with [blue-green-demo.md](blue-green-demo.md): this demo *produces* the artifact; that one
*ships* it safely. Run them back-to-back for a full "commit → running" story.

## Live URLs

Ports for the .NET services are assigned by the Aspire host per run; a typical local session:

| Surface | URL | Use in the demo |
| --- | --- | --- |
| web-admin | `http://localhost:7232` | Drive from **CI → Repositories / Pipelines / Builds** |
| Jenkins | `http://localhost:8080` | Raw stage logs / console B-roll |
| Nexus (raw + NuGet UI) | `http://localhost:8081` | Show the published artifacts |
| Nexus docker registry | `nexus:8082` | Where the resource images land |

## What "a build" actually is here

The platform models a **pipeline** as an ordered chain of Jenkins jobs, drives them through an
orchestrator, and syncs live stage status back into web-admin. For an Aspire app the key job is
**`cicd-aspire-publish`** (`jenkins/publish/aspire/Jenkinsfile`), which in one pass:

1. Clones `GIT_URL@GIT_BRANCH`; computes an **immutable version** `BASE_VER-ci.<build#>.g<sha>`
   and image tag `<build#>-<sha>`.
2. Uses **Aspir8** to build a container image per Aspire resource and push them to Nexus (`nexus:8082`).
3. **Trivy-scans** every image and uploads CycloneDX SBOMs, with a `FAIL_ON_SEVERITY` gate.
4. Tars the aspirate Kustomize output and **PUTs it to Nexus raw-hosted** — that archive *is* the
   manifest source the deployment service fetches.
5. Archives `build-info.json` and emits an **`AspireAppPublished`** event → the deployment service
   matches it to a registered app by source key and, if auto-deploy is on, **triggers a deploy**.

That last step is the hook into the blue-green demo: the build you just ran becomes the live deploy.

> The general (non-Aspire) chain is `cicd-build → cicd-scan → cicd-publish-nexus-{nuget,docker}`.
> `cicd-aspire-publish` collapses build + scan + publish into one job because Aspir8 owns the
> multi-container build/push. Job definitions: `jenkins/jobs/cicd-jobs.groovy`.

## Set the stage

Two panes:

1. **Browser** — web-admin `CI → Pipelines` (plus a tab on `Builds`).
2. **Jenkins tab** — `http://localhost:8080`, ready to open the running job's console for the
   "real logs" reveal.

Pre-check:

- A **Repository** registered (`CI → Repositories`) pointing at the app repo.
- A **Pipeline** wired to `cicd-aspire-publish`. The bundled `samples/aspire-sample` is the reference app.
- The target Aspire app registered in `Deployment → Aspire apps` with **auto-deploy on** and a
  **source key** matching the build's `APP_NAME` (so the handoff lands).

## The narrative (~6 min)

### Act 0 — the pitch (20s)
> "One commit, fully automated: build every Aspire container, scan it, publish immutable artifacts
> to Nexus, and hand off to deployment — with a security gate that can stop a bad build cold."

### Act 1 — trigger it (30s)
Pick your story:

- **Push-driven:** POST the normalized git webhook → it starts a pipeline run. Best "real world"
  beat. See [Webhooks locally](#webhooks-locally) for the exact curl (no cloud provider can reach the
  local port).
- **Click-driven:** `CI → Pipelines → Start` (safer for a live room — no waiting on webhooks).

### Act 2 — watch it flow (2 min)
On `CI → Pipeline runs` / `Builds`, narrate stages lighting up live (synced from Jenkins):
Checkout → Build + push images → **Scan + SBOM** → Publish manifest. Pop the Jenkins console for one
stage so the room sees it's real Jenkins underneath, not a mock.

### Act 3 — 🎯 the security gate (1.5 min)
The CI money shot. Point out the **Trivy scan** stage and the SBOMs in `SCA / Aspire SBOM`. Then
re-run with **`FAIL_ON_SEVERITY = critical`** against an image with a known CVE — the publish
**fails on purpose**, and no artifact is published.
> "A vulnerable build never reaches Nexus, let alone the cluster."

### Act 4 — the artifacts (1 min)
Open Nexus and show what landed:

- Per-resource **images** on `nexus:8082`, tagged `<build#>-<sha>` (immutable).
- The **manifest archive** in raw-hosted at `.../<app>/<version>/aspirate-output.tar.gz`.
- **NuGet** packages if the nuget publish job ran.
- `build-info.json` — version, commit, image tag, manifest URL.

> "Every artifact traces back to an exact commit; the tag is immutable."

### Act 5 — the handoff (1 min)
Because the app is registered with auto-deploy on, the publish fires `AspireAppPublished` → a deploy
kicks off on its own. Flip to `Deployment → Aspire apps` and watch a run appear with no further
clicks. **Hand straight into [blue-green-demo.md](blue-green-demo.md)** — the freshly built artifact
health-gates in a shadow namespace and cuts over.

### Closing line
> "Commit to running, scanned, immutable, and gated — the pipeline is the product, not a pile of scripts."

## Webhooks locally

"Webhooks" here means **two plain HTTP POSTs of normalized JSON** — no provider-specific signature
parsing, no inbound polling. (Everything *Jenkins → platform* — build status, pushed images — is the
opposite: `JenkinsBuildSyncService` polls on a timer. There is no Jenkins-fires-a-webhook path.)

| Hop | From → To | Endpoint | Fires on |
| --- | --- | --- | --- |
| 1 — git PR lifecycle | provider/curl → **jenkins-api** | `POST /api/jenkins/webhooks/git` | PR opened/closed |
| 2 — preview teardown | jenkins-api → **deployment-api** | `POST /api/deployment/previews/webhook` | PR close (internal) |

**The catch:** jenkins-api listens on a **dynamic localhost port** the Aspire host assigns (read it
from the dashboard; e.g. `http://localhost:7229`). It is **not publicly reachable**, and there's **no
local git server** in the AppHost — so a real GitHub/GitLab cloud webhook can't hit it. Hop 2 "just
works" because Aspire injects its base URL (`jenkins.WithEnvironment("Deployment__ApiBaseUrl", …)`) and
service discovery resolves it. Hop 1 you fire yourself.

The endpoint takes a **normalized, provider-agnostic** body (a thin adapter maps a raw GitHub/GitLab
payload onto it — or you POST it directly). There is **no HMAC/secret check**, and it always returns
200 with the outcome in the body. Routing: the repo must be **registered** (CI → Repositories) and of
kind **Aspire** (previews are Aspire-only); `opened/synchronize/reopened` on a **feature** branch
builds a preview (the **default** branch is skipped — that's a normal deploy), and `closed/merged`
tears the preview down via Hop 2.

Drive the whole preview lifecycle from the terminal (`PORT` = the jenkins-api port):

```bash
PORT=7229; REPO=aspire-sample; APP=sampleapp

# PR opened on a feature branch → build + spin up a per-PR preview environment
curl -sS -X POST "http://localhost:$PORT/api/jenkins/webhooks/git" \
  -H 'Content-Type: application/json' \
  -d "{\"repository\":\"$REPO\",\"branch\":\"feature/x\",\"action\":\"opened\",\"prNumber\":42,\"appName\":\"$APP\"}"
# → {"outcome":"build-triggered","runId":"..."}
#   other outcomes: default-branch-skipped | repo-not-found | not-an-aspire-repo | ignored-action:<a>

# PR closed → tear the preview down (jenkins-api calls the deployment teardown webhook)
curl -sS -X POST "http://localhost:$PORT/api/jenkins/webhooks/git" \
  -H 'Content-Type: application/json' \
  -d "{\"repository\":\"$REPO\",\"branch\":\"feature/x\",\"action\":\"closed\",\"prNumber\":42,\"appName\":\"$APP\"}"
# → {"outcome":"teardown-requested"}
```

Watch it land in `Deployment → Previews`: the `opened` call materializes an ephemeral namespace,
the `closed` call reaps it. For a real cloud provider, front the port with a tunnel (ngrok /
cloudflared) **and** supply an adapter, since the endpoint expects the normalized shape.

## Levers / fallback

- **No-Jenkins fallback:** if Jenkins stalls mid-demo, `samples/aspire-sample/publish-to-nexus.sh`
  produces the *exact same* Nexus artifacts (images + manifest archive) by hand — a reliable way to
  still trigger the handoff. Solid insurance for a live room.
  ```bash
  NEXUS_PASS='<pass>' APP_NAME=sampleapp APP_VERSION=1.0.0 ./samples/aspire-sample/publish-to-nexus.sh
  ```
- **Key knobs** (job parameters, defaults in the Jenkinsfile):
  | Param | What it controls |
  | --- | --- |
  | `BASE_VER` | Base version; `-ci.<build#>.g<sha>` is appended |
  | `FAIL_ON_SEVERITY` | `none` \| `high` \| `critical` — the vulnerability gate |
  | `APP_NAME` | Manifest artifact path segment (must match the app's source key) |
  | `NAMESPACE` | Kubernetes namespace baked into the manifests |
  | `GIT_BRANCH` | `main` deploys to the app's environment; other branches become previews |

## Talking points / honest caveats
- The container build needs the build agent to reach the **Docker socket** and resolve the `nexus`
  hostname — already wired in this local stack, but it's the thing that bites on a fresh host (the
  .NET SDK also rejects single-label registry hosts, hence the dotted `nexus:8082`).
- Version + image tag are **immutable** (build# + commit sha); re-running never overwrites an artifact.
- `cicd-aspire-publish` is the Aspire path; NuGet/plain-container apps use the `cicd-build → scan →
  publish-nexus-*` chain instead.
