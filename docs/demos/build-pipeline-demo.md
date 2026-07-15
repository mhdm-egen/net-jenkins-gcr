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

- **Push-driven:** push a commit → the git webhook starts a pipeline run. Best "real world" beat.
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
