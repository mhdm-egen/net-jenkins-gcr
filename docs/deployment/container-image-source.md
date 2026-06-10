# Container Image Source — Design

Decouples a deployment **Service** from a hard-coded container artifact URI by
introducing a reusable **container coordinate** that a service points at, plus a
**tag → digest** resolution step so deploys stay digest-pinned.

**Status:** design — no code yet. Open decisions in Section 7.
**Scope:** container images only. Generalizing to other artifact types is explicitly
**out of scope** (Section 8).

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

## 6. Structural delta

```text
Deployment.Domain
└─ Catalog/ (or Services/)
   └─ ContainerImage        new catalog entity (coordinate)
Service                     + optional ContainerImageId reference
Release                     unchanged shape (ArtifactUri still the resolved digest)
                            + optional: record the source tag for audit
```

Resolution is a thin port (`IContainerImageResolver` / similar) in front of release
creation: `(ContainerImage, tag) -> digest`. v1 may implement it as "the handoff
already supplies the digest" and add the live registry query as a follow-up
(Section 7, #1).

## 7. Open decisions (to resolve before code)

1. **Resolver scope now or later.** Ship the coordinate entity + digest-pin first
   (high value, low risk) and add the live tag→digest registry query as a second step,
   or build the resolver in v1?
2. **Tag-set semantics.** Are `DefaultTags` strictly the *default selector*, or a set of
   *acceptable aliases / metadata*? Leaning: a default selector; the actual tag is
   chosen per release/deploy.
3. **Cross-registry identity.** One logical image often exists in both Nexus (source) and
   GAR (promoted). Model as one `ContainerImage` with the GAR ref *derived* (as the Cloud
   Run adapter already does), or as two rows? Leaning: derive, don't duplicate.

## 8. Out of scope (deliberately not over-designed)

- **Other artifact types** (NuGet, Zip, Manifest/Application). `Release` is a tagged
  union over `ArtifactType`; those types address and pin differently (NuGet pins on
  version, Zip on checksum, Manifest has no artifact at all) and not all are deployable
  to a runtime target. A general `IArtifactSource` family and a `TargetKind × ArtifactType`
  compatibility matrix are the eventual home for that, but **not now** — this doc covers
  the container coordinate only.
- **Production tag policy** (forbid `latest`, require pinned tag) — noted in Section 3,
  not decided.
- **Canary / blue-green** traffic splitting — unrelated; tracked elsewhere.
