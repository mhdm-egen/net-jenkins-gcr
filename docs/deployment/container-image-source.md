# Container Image Source — Design

Decouples a deployment **Service** from a hard-coded container artifact URI by
introducing a reusable **container coordinate** that a service points at, plus a
**tag → digest** resolution step so deploys stay digest-pinned.

**Status:** v1 ready for implementation — decisions resolved (Section 8). No code yet.
**Scope:** container images only. Generalizing to other artifact types is explicitly
**out of scope** (Section 9).

---

## 1. Problem

Today a container release's address is captured as a single literal string —
`Release.ArtifactUri` (e.g. `nexus:8082/docker-private/orders-api@sha256:…`). A
service that deploys a container has no reusable notion of *which image* it is; the
URI is hand-typed (Map dialog, `Deploy-CloudRun.ps1 -Image …`) or stamped at handoff
time. Consequences:

- No "pick the image from a list" UX — every entry retypes registry/repo/name.
- The registry coordinate (host + repo + image name) is duplicated across releases
  and scripts instead of being defined once.
- Switching a service's backing image, or its registry, means editing strings in
  several places.

## 2. Proposal

Introduce a first-class **`ContainerImage`** catalog entity (the *coordinate*) and let
a `Service` reference one. A release/deploy then **selects a coordinate + a tag**
(default `latest`); the system **resolves the tag to a digest** and constructs the
immutable `ArtifactUri` from the coordinate. The Service is no longer bound to a URI —
it is bound to a coordinate, and the concrete artifact is resolved at
release/deploy time.

```text
ContainerImage (deployment catalog entity)
  Id
  Name                 // logical image, e.g. "orders-api"
  Registry             // host, e.g. nexus:8082
  Repository           // path within the registry, e.g. docker-private
  DefaultTags = ["latest"]
  IsActive             // deactivate to hide from pickers; existing releases unaffected
  // BaseRef()   => {Registry}/{Repository}/{Name}
  // Resolve(tag) => {Registry}/{Repository}/{Name}@sha256:<digest>
```

`Service` gains an optional `ContainerImageId`. The Map / publish-release UI then
**selects from the list of `ContainerImage` rows** instead of typing a URI.

## 3. The load-bearing invariant — resolve tags to a digest, deploy by digest

A tag (`latest`, `stable`, `v1.2`) is a **selector**, never the deploy target. The
selected tag is resolved against the registry (Nexus is the system of record; GAR for
promoted images) to a concrete digest, and **that digest** is what the `Release`
stores and the adapter deploys.

```text
select ContainerImage + tag (default "latest")
        │  query registry → digest
        ▼
ArtifactUri = {Registry}/{Repository}/{Name}@sha256:<digest>   ← recorded on Release
        │  (also record which tag it resolved from, for audit)
        ▼
deploy (Cloud Run adapter is unchanged — it already takes a digest ref)
```

This preserves the existing digest-pinned chain end to end: immutable releases,
meaningful effective-versions dashboard, SBOM/vuln provenance bound to an exact
artifact, and reliable rollback. `latest` is a convenience for selection only; it is
pinned the moment a release is created. (For production environments we may later
require an explicit tag / forbid bare `latest`, hung off the existing `IsProduction` +
approval gates — noted, not decided.)

## 4. How it plugs in

- **Handoff / auto-publish (happy path):** already knows the produced container and its
  Nexus digest, so it links/creates the `ContainerImage` and stamps the release digest
  directly — no tag guessing needed.
- **Manual / ad-hoc deploy** (and `Deploy-CloudRun.ps1`): this is where tag→digest
  resolution earns its keep — pick the image, say `latest` (or a specific tag), the
  resolver returns the digest.
- **Cloud Run adapter:** unaffected. It receives a digest `ArtifactUri` as before and
  does not care that a coordinate + tag produced it.
- **`Release.ArtifactUri` stays the canonical, resolved, immutable value.** The
  coordinate/selector lives *in front of* release creation; the release record stays a
  flat immutable snapshot.

## 5. Relationship to existing model

The CI side already has `DeployableComponent.ContainerName` — a bare-string "this repo
produces container X" mapped to a deployment service. `ContainerImage` is the richer
form of that string: the same association, upgraded from a name into a reference to a
real coordinate (registry + repo + name). The natural evolution is for the
component mapping / service to reference a `ContainerImage` rather than carry a loose
name + URI.

## 6. Lifecycle

**Enabling property:** because the design resolves tag → digest and stores the resolved
`ArtifactUri` on the **immutable Release** (Section 3), a `ContainerImage` coordinate is
only needed at *authoring* time. Existing releases never depend on it at runtime — they
carry their own pinned digest. So coordinates are **cheap to create and safe to retire**:
hiding or deleting one can never break a release that already used it. This drives the
whole lifecycle.

### Creation — discovered first, authored as fallback

The system usually already knows the coordinate, so users shouldn't hand-author master
data that drifts. Three triggers, in priority order:

1. **Auto-upsert from the CI handoff / component mapping** *(authoritative).* The handoff
   already supplies registry + repo + name + digest, and `DeployableComponent.ContainerName`
   already says "this repo produces container X." First time a container flows through,
   upsert the `ContainerImage`. No manual entry, no drift.
2. **Dynamic discovery in the release modal.** Nexus is the system of record for what
   images/tags exist, so the modal queries it: pick image name → pick tag → resolve to
   digest. Selecting materializes/uses a coordinate. Covers ad-hoc releases of images not
   flowing through a mapped component.
3. **Explicit create** *(escape hatch).* For what the system can't discover — an external
   registry, or pre-registration before the first build. Rare.

The release-modal dropdown is populated from **known coordinates (#1) + a live registry
query (#2)**, with explicit-create for the long tail.

### Tags — live-query, don't freeze a list

The coordinate stores the **stable address** (registry + repo + name) plus a **default
selector** (`latest`). The *available* tags come from the live Nexus query at modal time;
a frozen tag list on the entity would reintroduce the drift we're avoiding. The chosen tag
is pinned to a digest on the release.

### Retirement — deactivate, don't delete

Mirror the rest of the system (`Service`, `SourceRepository`, `DeployableComponent` all use
`IsActive` + Deactivate/Reactivate):

- **Deactivate** → the coordinate drops out of the release-modal dropdown and discovery,
  but **existing releases are untouched** (they hold their own digest). The normal "stop
  offering this image" action.
- **Hard delete** only as optional cleanup for a coordinate **no release ever referenced**.
  If anything referenced it, deactivate is the only safe verb.

The resolve-to-digest property means there's never a "can't delete, it's in use" block at
*runtime* — only authoring history, which deactivate handles.

### UI placement

- **Primary surface = the release-modal dropdown** (select known + live-discovered) — where
  almost all interaction happens.
- **Management = a light catalog**, not a heavy nav item: either a small
  `/deployment/container-images` list with activate/deactivate (consistent with
  Services/Environments), or folded into **Service detail** as "backing image." Start
  folded-in unless many shared/standalone images are expected, then promote to its own page.

## 7. Structural delta

```text
Deployment.Domain
└─ Catalog/ (or Services/)
   └─ ContainerImage        new catalog entity (coordinate; Registry/Repository/Name,
                            DefaultTags, IsActive)
Service                     + optional ContainerImageId reference
Release                     unchanged shape (ArtifactUri still the resolved digest)
                            + record the source tag the digest was resolved from (audit)
```

Resolution is a port — `IContainerImageResolver` `(ContainerImage, tag) -> digest` — in
front of release creation, and is **in scope for v1** (decision #2). Two paths feed it:
the **handoff** auto-publish path supplies the digest directly (no query needed), and the
**release modal / manual** path calls the resolver to live-query Nexus (system of record;
GAR ref derived for promoted images per decision #4). The Cloud Run adapter is unaffected —
it still consumes the resolved digest `ArtifactUri`.

## 8. Resolved decisions

| # | Decision | Resolution |
| --- | --- | --- |
| 1 | Creation model | **Auto-materialize** (CI handoff/component upsert + live Nexus discovery); explicit-create as escape hatch |
| 2 | Resolver scope | **Live tag→digest resolver in v1** |
| 3 | Tag-set semantics | **Stable address + default selector; tags live-queried** (no persisted tag list) |
| 4 | Cross-registry identity | **One entity, GAR ref derived** (not duplicated) |

1. **Creation model — auto-materialize.** Coordinates are upserted automatically from the
   CI handoff / component mapping and from live Nexus discovery in the release modal;
   explicit-create is the escape hatch for external/undiscoverable images. The catalog is a
   projection of CI + Nexus, not hand-maintained master data (Section 6).
2. **Resolver scope — in v1.** Build the coordinate entity *and* the live Nexus tag→digest
   resolver together, so the release modal can dynamically discover and resolve any
   image+tag from day one (not deferred). The auto-publish/handoff path still supplies the
   digest directly; the resolver serves the manual/discovery path (Section 7).
3. **Tag-set semantics — default selector + live tags.** The entity stores the stable
   address plus a single default selector (`latest`); available tags are live-queried from
   Nexus at modal time and the chosen tag is pinned to a digest on the release. No frozen
   tag list on the entity.
4. **Cross-registry identity — one entity, GAR derived.** A single `ContainerImage` keyed on
   the Nexus source coordinate; the GAR ref is derived at deploy time (as the Cloud Run
   adapter already does). No duplicate rows.

**Still deferred (not blocking v1):** production tag policy (forbid bare `latest` / require a
pinned tag, hung off `IsProduction` + approval gates) — Section 3.

## 9. Out of scope (deliberately not over-designed)

- **Other artifact types** (NuGet, Zip, Manifest/Application). `Release` is a tagged
  union over `ArtifactType`; those types address and pin differently (NuGet pins on
  version, Zip on checksum, Manifest has no artifact at all) and not all are deployable
  to a runtime target. A general `IArtifactSource` family and a `TargetKind × ArtifactType`
  compatibility matrix are the eventual home for that, but **not now** — this doc covers
  the container coordinate only.
- **Production tag policy** (forbid `latest`, require pinned tag) — noted in Section 3,
  not decided.
- **Canary / blue-green** traffic splitting — unrelated; tracked elsewhere.
