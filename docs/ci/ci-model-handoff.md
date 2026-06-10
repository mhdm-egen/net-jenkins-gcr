# Design Handoff: Jenkins CI / Build-Provenance Data Model

You are a senior .NET developer and DevOps engineer joining an in-progress design
effort. **Treat this document as the source of truth.** Read it fully, then see
"Your task" at the end. Its companion, [ci-model-decisions.md](./ci-model-decisions.md),
records the resolved decisions; read both together — this is the design intent, that
is the resolved-state snapshot.

---

## 1. Goal

Design (and eventually implement) a data model for the **Jenkins CI microservice**:
track a change to a source repository as it flows through a build, gets assembled into
artifacts, and is pushed to Nexus as **both assemblies (NuGet) and containers**. The
model must answer "what commit produced which artifacts, with what versions, pushed
where, and is it deployed?" — and must **integrate certain repos' container builds into
the deployment microservice** as `Release`s.

This service is the symmetric counterpart to the deployment service: **CI owns build
and artifact provenance; deployment owns releases and deployments.** The only coupling
is a one-way HTTP handoff (Section 7).

## 2. Governing principles

- **The build is the unit of truth.** Everything traces back to one CI build of one
  commit. Versions, artifacts, SBOM, and vulnerability reports all hang off that build.
- **The artifact of record lives in Nexus.** Nexus is the canonical, general-purpose
  registry (NuGet hosted + Docker hosted). Container images are identified **by digest**,
  not by floating tag.
- **CI knows nothing about clouds.** Promotion of an image into a cloud-target registry
  (GAR) and the deploy itself are the deployment service's concern (decision #6). CI
  holds no GCP credentials.
- **The handoff is one-way and explicit.** CI calls the deployment service's `Releases`
  API and stores the returned `ReleaseId` as a foreign reference. Neither service reads
  or writes the other's database.

## 3. Core concepts

- **SourceRepository** — a tracked source repo. First-class here (it did not exist as a
  persisted concept before). Owns its CI identity (which Jenkins job builds it) and its
  deployment wiring (which containers map to which deployment Services).
- **DeployableComponent** — *the container→deployment mapping*. A repo can produce
  **several** deployable container images; each one that should integrate with deployment
  maps (by container image name) to a `DeployableUnitId` in the deployment service. Holds
  a per-component `AutoPublish` flag.
- **Build** — one CI run of one commit. Carries the resolved versions (the `build-info.json`
  block), the source revision, status/timing, and the SBOM / vulnerability report
  locations. Parent of the artifacts it produced.
- **BuildArtifact** — a thing the build produced: a **NuGet package** or a **container
  image**. The point where "assemblies vs containers" diverge, via `ArtifactKind`.
- **ArtifactPublication** — a push of an artifact to a registry (Nexus NuGet / Nexus
  Docker). Records the immutable reference (digest for images) and tags.
- **ContainerReleaseHandoff** — *the integration record*. Durable proof that "build X of
  repo Y, container C, became `Release` Z in the deployment service." Holds the foreign
  `DeploymentReleaseId`. This is the seam.

## 4. Entities and attributes

Illustrative types use `guid` PKs and string enums. Value objects (VO) are owned/embedded.

### SourceRepository (aggregate root)

*(Named `SourceRepository` in code to avoid colliding with the persistence repository pattern `IRepository`.)*

- `RepositoryId` (PK)
- `Name` (unique)
- `GitUrl`
- `Provider` — `GitHub | AzureDevOps | GitLab | Bitbucket | Other`
- `DefaultBranch`
- `CiJobName` — the Jenkins job that builds it (e.g. `cicd-build`)
- `BaseVersion` — the `BASE_VER` parameter that seeds version derivation
- `IsActive`
- `CreatedAtUtc`

### DeployableComponent (child of SourceRepository; the deployment mapping)
- `DeployableComponentId` (PK)
- `RepositoryId` (FK → SourceRepository)
- `ContainerName` — the image name the build produces (e.g. `egen/web-apphost`); **the match key**
- `DeployableUnitId` — the Service id in the **deployment** microservice
- `DeployableUnitName` — cached label for display
- `AutoPublish` — auto-handoff on a successful container push (else operator-promoted)
- `IsActive`
- Unique on (`RepositoryId`, `ContainerName`)

### Build (aggregate root)
- `BuildId` (PK)
- `RepositoryId` (FK → SourceRepository)
- `CiJobName`, `CiBuildNumber` — natural CI key (e.g. `cicd-build` / `42`)
- `CiRunUrl` — clickable (`http://jenkins:8080/job/cicd-build/42/`)
- `CiRunId` — programmatic (`cicd-build/#42`)
- `SourceRevision` (VO) — `CommitSha`, `CommitShort`, `Branch`, `Author?`, `Message?`, `CommittedAtUtc?`
- `Versions` (VO) — `PackageVersion`, `FileVersion`, `AssemblyVersion`, `InformationalVersion`, `BaseVersion` (the `build-info.json` block)
- `Status` — `Queued | Running | Succeeded | Failed | Aborted`
- `StartedAtUtc`, `CompletedAtUtc`, `DurationMs`
- `Quality` (VO) — `SbomUri`, `VulnerabilityReportUri` (the Nexus-raw `bom-vex.json` / `vulnerabilities.json` locations)
- `TriggeredBy`
- Unique on (`CiJobName`, `CiBuildNumber`)

### BuildArtifact (child of Build)
- `BuildArtifactId` (PK)
- `BuildId` (FK → Build)
- `ArtifactKind` — `NuGetPackage | ContainerImage`
- `Name` — package id (`Egen.Foo`) or image repo (`egen/web-apphost`)
- `Version` — `PackageVersion` for NuGet; primary semantic tag for an image
- `Digest` — `sha256:…` for an image; `.nupkg` hash for a package (integrity)
- `SizeBytes`
- `ProducedAtUtc`

### ArtifactPublication (child of BuildArtifact)
- `PublicationId` (PK)
- `BuildArtifactId` (FK → BuildArtifact)
- `Registry` — `NexusNuGet | NexusDocker` (GAR is **not** here — see decision #6)
- `Reference` — the immutable coordinate: `host/path@sha256:…` for images; feed URL + id/version for NuGet
- `Tags` — for images, the tri-tag set `["latest", "ci-42", "7a4b9c1"]`
- `Status` — `Pushed | Failed`
- `PublishedAtUtc`

### ContainerReleaseHandoff (aggregate root; the integration record)
- `HandoffId` (PK)
- `BuildId` (FK → Build)
- `BuildArtifactId` (FK → BuildArtifact; the container that was handed off)
- `DeployableComponentId` (FK → DeployableComponent; the mapping that matched)
- `RepositoryId`
- `DeployableUnitId` — the deployment Service id targeted
- `DeploymentReleaseId` — **the `Release.Id` returned by the deployment service** (the only foreign handle)
- `SemanticVersion` — what was sent (the `PackageVersion`)
- `ArtifactUri` — the **Nexus** digest ref sent as the release artifact
- `Status` — `Pending | Published | Failed | Skipped`
- `RequestedByPrincipal`
- `HandoffAtUtc`
- `FailureReason?`

## 5. Relationships (cardinalities)

- SourceRepository 1—* DeployableComponent
- SourceRepository 1—* Build
- Build 1—* BuildArtifact
- BuildArtifact 1—* ArtifactPublication
- Build 1—* ContainerReleaseHandoff  (one per promoted container artifact)
- DeployableComponent 1—* ContainerReleaseHandoff
- ContainerReleaseHandoff —*reference*→ deployment `Release` (cross-service; **no FK**)

## 6. Required queries (acceptance criteria for the model)

**Q1 — Reverse provenance of a deployed release.** Given a `DeploymentReleaseId`, return
the `Build`, its commit, all artifacts, and the SBOM / vulnerability URIs. ("What
actually went into Release Z?")

**Q2 — Build history of a repo.** All `Build` rows for a repo, newest first, with status,
`PackageVersion`, and commit short — the "changes through builds" timeline.

**Q3 — Deployment surface of a repo.** The `DeployableComponent` rows: which containers
this repo produces and which deployment Service each maps to.

**Q4 — Promotion backlog.** Successful container builds of integration-enabled repos that
have **no** `ContainerReleaseHandoff` with `Status = Published` — i.e. green builds not
yet promoted.

**Q5 — Full trace.** commit → build → artifacts → publications → handoff(s) → release id,
in one walk.

## 7. The microservice boundary & integration with deployment

CI → Deployment is a **one-way HTTP handoff** over `Deployment.Contracts`. For a container
`BuildArtifact` whose `Name` matches a `DeployableComponent.ContainerName`, when
`AutoPublish` is set (or an operator promotes), the CI service calls, in order:

1. `POST /api/deployment/releases` — `PublishReleaseRequest`
2. `POST /api/deployment/releases/{id}/provenance` — `AttachProvenanceRequest`

and persists the returned id as `ContainerReleaseHandoff.DeploymentReleaseId`.

**Field mapping (CI → deployment Release):**

| Deployment field | Source in CI |
| --- | --- |
| `DeployableUnitId` | `DeployableComponent.DeployableUnitId` |
| `SemanticVersion` | `Build.Versions.PackageVersion` |
| `BuildNumber` | `Build.CiBuildNumber` (formatted, e.g. `#42`) |
| `CommitSha` | `Build.SourceRevision.CommitSha` |
| `ArtifactType` | `ContainerImage` (wire value `2`) |
| `ArtifactUri` | the **Nexus** Docker digest ref (`ArtifactPublication.Reference`) |
| `ArtifactSha256` | `BuildArtifact.Digest` |
| `SbomUri` | `Build.Quality.SbomUri` |
| `VulnerabilityReportUri` | `Build.Quality.VulnerabilityReportUri` |
| `CiRunUrl` | `Build.CiRunUrl` |
| `CiRunId` | `Build.CiRunId` |
| `PublishedByPrincipal` | `ContainerReleaseHandoff.RequestedByPrincipal` (or a system account) |

**Idempotency.** The deployment side rejects a duplicate `(DeployableUnitId,
SemanticVersion)`. Because `PackageVersion` is unique per build (decision #4), a re-fired
handoff is naturally idempotent: on a 409, the CI service records the existing release id
rather than failing. The handoff row's `Status` is the retry anchor.

**Note (decision #6):** the `ArtifactUri` deliberately points at **Nexus**, not GAR. The
deployment service is responsible for promoting the image into GAR (by digest) and
deploying the GAR ref for GCP targets. CI's job ends at Nexus.

## 8. Proposed implementation stack (confirm before coding)

Mirrors the deployment service for symmetry — confirm in [ci-model-decisions.md](./ci-model-decisions.md) §11.

- .NET 10 / C# 13 / EF Core 10
- SQLite (same provider the deployment service ships with)
- Wolverine for in-process domain-event dispatch after `SaveChangesAsync`
- xUnit + FluentAssertions
- Minimal-API surface (`Jenkins.Api`), Clean Architecture split
  (`Jenkins.Domain` / `Jenkins.Application` / `Jenkins.Infrastructure` / `Jenkins.Contracts`)
- **Reuse** the existing `Jenkins.Client` (talk to Jenkins) and
  `Jenkins.Orchestrator(.Abstractions)` as infrastructure libraries
- `Jenkins.Infrastructure` references `Deployment.Contracts` for the handoff client
  (`IDeploymentReleaseClient`)

## 9. Your task

1. Restate the model and surface any ambiguities before writing code.
2. The eight open dimensions are already resolved — see [ci-model-decisions.md](./ci-model-decisions.md).
3. **Do NOT generate EF entities, a DbContext, migrations, or DDL until asked.**
4. When asked to implement, follow Section 8 and the decisions doc's implementation order.
