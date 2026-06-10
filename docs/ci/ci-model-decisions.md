# CI Model — Decisions Resolved

Companion to [ci-model-handoff.md](./ci-model-handoff.md). Captures every decision made
while designing the Jenkins CI / build-provenance microservice. Read both together: the
handoff is the design intent; this is the resolved-state snapshot.

**Status:** v1 ready for implementation. No code generated yet — see Section 9.

---

## 1. Scope

This document resolves the six design decisions for the CI microservice. Each section
states the decision, the reasoning, the schema/structural delta, and implementation hooks.
All decisions apply to **v1**; anything deferred is called out in Section 8.

## 2. Quick-reference matrix

| # | Decision | Resolution | Delta |
| --- | --- | --- | --- |
| 1 | Service home | **New Jenkins CI microservice** (`Jenkins.Api` + Domain/Application/Infrastructure, own DbContext) | New solution / 5 projects |
| 2 | Repo → deployable cardinality | **Multiple containers per repo** | `DeployableComponent` child entity |
| 3 | Handoff trigger | **Manual promote; per-component `AutoPublish` opt-in** | 1 column on `DeployableComponent` |
| 4 | Release `SemanticVersion` | **Full `PackageVersion`** (`1.0.0-ci.42.g7a4b9c1`) | None (mapping rule) |
| 5 | Commit representation | **`SourceRevision` value object** on `Build` | Owned VO, no table |
| 6 | GAR/GCR ownership | **Moves to the deployment service** | Removes GAR from CI; adds promoter to deployment |

---

## 3. Decision #1 — Service home: a real microservice

**Decision:** Stand up a dedicated CI microservice (`Jenkins.Api` + `Jenkins.Domain` /
`Jenkins.Application` / `Jenkins.Infrastructure` / `Jenkins.Contracts`) with its own
EF Core DbContext, mirroring the `Deployment.*` layout. **Not** an extension of the web
UI's `BuildSyncDbContext`.

**Reasoning:** We are treating Jenkins and deployment as peer microservices. Persisting
build/provenance inside the Blazor admin UI would make "the CI microservice" literally be
the web app, blurring the very boundary we are drawing. A standalone service is the
symmetric counterpart to the deployment service and gives the UI and the Jenkinsfiles a
single HTTP system-of-record to call.

**Structural delta:**

```text
src/jenkins/   (new projects beside the existing Jenkins.* libs)
├─ Jenkins.Domain          aggregates + VOs + enums + domain events
├─ Jenkins.Application      features (record build/artifact/publication, promote handoff)
│                          + IDeploymentReleaseClient port
├─ Jenkins.Infrastructure   EF Core DbContext + repos; wraps Jenkins.Client;
│                          DeploymentReleaseClient (HTTP, consumes Deployment.Contracts)
├─ Jenkins.Contracts        wire DTOs the UI consumes
└─ Jenkins.Api              minimal-API surface
   reuse: Jenkins.Client, Jenkins.Orchestrator(.Abstractions)
```

**Implementation hooks:** the web UI (`cicd.web.admin`) becomes a **client** of
`Jenkins.Api`; its current `BuildSyncDbContext` / `BuildRunRecord` store is superseded.
Migrating existing sync rows is optional (the data re-syncs from Jenkins).

**Naming note:** "Jenkins" is the tool, not the domain; `Build.*` or `Ci.*` would describe
the service better. Kept `Jenkins.*` for consistency with the existing libraries — rename
is a free swap if preferred before code lands.

---

## 4. Decision #2 — Multiple containers per repo

**Decision:** A tracked `SourceRepository` can produce several deployable container images. The
container→deployment mapping is a first-class child entity, `DeployableComponent`, keyed by
container image name.

**Reasoning:** The current pipeline is ~1 container ("apphost") per repo, but a solution
routinely builds several deployable projects (API + worker + …). Modeling the mapping as a
collection from day one avoids a schema rework; a single-container repo is just the N=1 case.

**Schema delta — new child entity:**

```text
DeployableComponent
  DeployableComponentId   PK
  RepositoryId            FK → SourceRepository
  ContainerName           string   -- match key against BuildArtifact.Name
  DeployableUnitId        guid     -- Service id in the deployment microservice
  DeployableUnitName      string   -- cached label
  AutoPublish             bool     -- see #3
  IsActive                bool
  -- unique (RepositoryId, ContainerName)
```

A repo with **zero** `DeployableComponent` rows is simply not wired to deployment. The
`SourceRepository` aggregate therefore carries no deployment-specific columns itself.

**Implementation hooks:** handoff resolution matches each container `BuildArtifact.Name`
against `DeployableComponent.ContainerName` within the build's repo. A build artifact with
no matching component is built and published to Nexus but never handed off.

---

## 5. Decision #3 — Handoff trigger: manual, with per-component auto opt-in

**Decision:** By default a successful container build does **not** auto-create a deployment
`Release`; an operator promotes it. A per-`DeployableComponent` `AutoPublish` flag opts an
individual container into hands-off promotion.

**Reasoning:** Manual-by-default is the safe posture for anything heading to prod. Putting
the flag on `DeployableComponent` (not `SourceRepository`) is strictly more granular than the
original "per-repo" framing — one repo can auto-publish its API while gating its worker.

**Schema delta:** the `AutoPublish` column on `DeployableComponent` (already listed in #2).

**Implementation hooks:**
- `PromoteToReleaseHandler` (Application) is callable two ways: invoked explicitly by an
  operator action, or fired automatically by a domain-event handler on
  `ContainerPublished` **when** the matching component has `AutoPublish = true`.
- Every promotion — auto or manual — writes a `ContainerReleaseHandoff` row, so the audit
  trail is identical regardless of trigger.

---

## 6. Decision #4 — Release SemanticVersion = full PackageVersion

**Decision:** The CI service sends `Build.Versions.PackageVersion` (e.g.
`1.0.0-ci.42.g7a4b9c1`) as the deployment `Release.SemanticVersion`.

**Reasoning:** The deployment `Release` is unique on `(DeployableUnitId, SemanticVersion)`.
`PackageVersion` is already unique per build and embeds the build number and commit short —
so it satisfies the constraint with no extra work and makes the handoff naturally
idempotent. A "clean" base version (`1.0.0`) would collide across builds and force a
per-build suffix anyway, reintroducing exactly the CI metadata it tried to drop.

**Schema delta:** none — a mapping rule (see handoff §7). `PackageVersion` is already
captured in the `Versions` VO.

**Implementation hooks:** on a `409 Conflict` from `POST /releases` (duplicate version),
the handoff handler treats it as already-published: it records the existing release id and
marks the handoff `Published` rather than `Failed`.

---

## 7. Decision #5 — Commit as a value object

**Decision:** Model the commit as a `SourceRevision` value object embedded on `Build`
(`CommitSha`, `CommitShort`, `Branch`, `Author?`, `Message?`, `CommittedAtUtc?`), not a
separate `Commit` aggregate.

**Reasoning:** A commit only becomes interesting in this model once it is built, and
`build-info.json` already carries the commit fields per build. A VO keeps the schema flat
and matches that artifact one-to-one.

**Schema delta:** owned VO → columns on the `Build` table (`Commit_Sha`, `Commit_Short`,
…). No separate table.

**Deferred (see §8):** promote `Commit` to its own entity only if "commits not yet built"
or "all builds of commit X across repos" becomes a first-class query.

---

## 8. Decision #6 — GAR/GCR ownership moves to deployment

**Decision:** The Nexus→GAR image promotion and the Cloud Run / GKE deploy are owned by the
**deployment** microservice, not CI. CI's container responsibility ends at the **Nexus**
Docker push. The `ContainerReleaseHandoff` (and the `Release.ArtifactUri`) therefore
reference the **Nexus** digest.

**Reasoning:** Build-once/deploy-many. The Nexus image is the canonical, target-neutral
artifact; GAR is needed *only* because the target is a GCP compute platform (Cloud Run,
GKE), so it is a target-reachability detail, not a build output. Cloud Run and GKE share
the same GAR need, so it belongs once behind the deployment service's GCP target family
rather than baked into the CI pipeline. Bonus: CI sheds all GCP credentials — a compromised
build can no longer touch the GCP project (least privilege / blast radius). Confirmed that
GAR is **not** general-purpose here (Nexus is), which is what makes deployment ownership the
right call.

**Delta — CI side:**
- `ArtifactPublication.Registry` enum is `NexusNuGet | NexusDocker` only (no `GAR`).
- The `cicd-publish-gcp-gar` and `cicd-publish-gcp-gcr` Jenkins stages leave CI entirely.
  The CI pipeline ends at `cicd-build → cicd-publish-nexus-{nuget,docker}`.

**Delta — deployment side (tracked here, specified in the deployment model when built):**
- A `GoogleArtifactRegistry` promoter (`IArtifactPromoter` / `IGcpImagePromoter`) used by the
  GCP target adapters: an idempotent "ensure image present in GAR by digest" pre-step that
  copies from Nexus, digest-preserving.
- GAR repo coordinates on the GCP `DeploymentTarget` (project/region already present; add the
  Artifact Registry repo) so the promoter knows the destination.
- The deployment runner gains an image-copy capability (crane/skopeo or the registry API)
  plus **Nexus read creds** and **GAR write creds** — the operational cost of this move.
- `Release.ArtifactUri` carries the Nexus canonical digest ref; the GCP adapter resolves it
  to the GAR ref it actually deployed and records that (a `DeploymentEvent` or a
  `ResolvedImageRef`), keeping the `Release` target-neutral.

---

## 9. Consolidated structure (one place to look)

### New aggregates / entities (CI service)

| Entity | Role |
| --- | --- |
| `SourceRepository` (root) | tracked source repo + CI identity |
| `DeployableComponent` (child) | container→deployment-Service mapping (+ `AutoPublish`) |
| `Build` (root) | one CI run of one commit; owns `SourceRevision`, `Versions`, `Quality` VOs |
| `BuildArtifact` (child) | a produced NuGet package or container image |
| `ArtifactPublication` (child) | a push to Nexus (NuGet or Docker) |
| `ContainerReleaseHandoff` (root) | the integration record; holds `DeploymentReleaseId` |

### Enums

| Enum | Values |
| --- | --- |
| `RepositoryProvider` | GitHub, AzureDevOps, GitLab, Bitbucket, Other |
| `BuildStatus` | Queued, Running, Succeeded, Failed, Aborted |
| `ArtifactKind` | NuGetPackage, ContainerImage |
| `PublicationRegistry` | NexusNuGet, NexusDocker |
| `PublicationStatus` | Pushed, Failed |
| `HandoffStatus` | Pending, Published, Failed, Skipped |

### Key constraints / indexes

- Unique `SourceRepository(Name)`.
- Unique `DeployableComponent(RepositoryId, ContainerName)`.
- Unique `Build(CiJobName, CiBuildNumber)` — natural CI key for upserts.
- Index `Build(RepositoryId, StartedAtUtc DESC)` — Q2 history.
- Index `ContainerReleaseHandoff(DeploymentReleaseId)` — Q1 reverse provenance.
- Index `ContainerReleaseHandoff(BuildId, Status)` — Q4 promotion backlog.

### Cross-service edge

`Jenkins.Infrastructure` → `Deployment.Contracts` (HTTP client only). No shared database,
no reverse dependency. `ContainerReleaseHandoff.DeploymentReleaseId` is a value reference,
not a foreign key.

---

## 10. Deferred to v2

| Deferred item | Trigger for v2 |
| --- | --- |
| `Commit` as its own entity | Need "commits not yet built" / cross-repo commit queries |
| In-CI registry promotion / extra `PublicationRegistry` values | A second general-purpose registry appears (still not GAR) |
| Handoff to **Application** releases (BOM), not just Service containers | Need to promote a whole app manifest from CI |
| Webhook/event ingestion (push-triggered builds) vs. Jenkins polling | Build volume makes polling costly |
| Per-build artifact retention / GC policy | Nexus retention needs to be mirrored/driven from here |

---

## 11. What happens next

**Confirmed v1 stack** (handoff §8): .NET 10 / C# 13 / EF Core 10, SQLite, Wolverine for
domain events, xUnit + FluentAssertions, minimal API, Clean Architecture split. Reuse
`Jenkins.Client`; reference `Deployment.Contracts` for the handoff client.

**Suggested implementation order** (each a PR-sized chunk):

1. **Solution + projects** — scaffold the 5 projects + solution, references, empty DbContext.
2. **SourceRepository aggregate** — `SourceRepository` + `DeployableComponent` (identity + mapping).
3. **Build aggregate** — `Build` + `BuildArtifact` + `ArtifactPublication` with the VOs.
4. **Ingestion features** — record/upsert a build and its artifacts/publications (fed by the
   build-sync path that today populates `BuildRunRecord`).
5. **Handoff feature** — `PromoteToRelease` + `ContainerReleaseHandoff`, the
   `IDeploymentReleaseClient` HTTP adapter, and the `AutoPublish` event handler.
6. **API surface** — `Jenkins.Api` endpoints (repos, builds, components, promote).
7. **Initial EF migration** — single migration; SQLite.
8. **UI cutover** — point `cicd.web.admin` at `Jenkins.Api`; retire `BuildSyncDbContext`.
9. **Deployment-side #6** — the GAR promoter + GCP target config (separate, in the
   deployment service).

No PR should introduce code for any item in the "Deferred to v2" list (§10).
