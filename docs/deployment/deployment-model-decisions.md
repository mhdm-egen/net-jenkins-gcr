# Deployment Model — Decisions Resolved

Companion to [deployment-model-handoff.md](./deployment-model-handoff.md). Captures every decision made while working through Section 7's open dimensions. Read both documents together: the handoff is the design intent; this one is the resolved-state snapshot.

**Status:** v1 ready for implementation. No code generated yet — see Section 11 for what happens next.

---

## 1. Scope

This document resolves the eight open dimensions in Section 7 of the handoff. Each section below:

- States the decision (one sentence).
- Lays out the reasoning that got us there.
- Specifies the schema delta (new tables, new columns, enum extensions).
- Calls out implementation hooks (which Application feature folder, which domain event, etc.).

All decisions apply to **v1**. Anything explicitly deferred to v2 is called out separately in Section 10.

---

## 2. Quick-reference matrix

| # | Open item | Decision | Schema delta |
| --- | --- | --- | --- |
| 1 | Current-pin fallback | Fall back to `Latest`; hard-fail if catalog empty for that service | None |
| 2 | Config precedence | Rule X ladder; versioned history via domain events; secret bindings snapshotted per deployment | 1 new table (history), 1 new entity (bindings) |
| 3 | Ordering / compatibility / failure | Catalog-level order; no compatibility rules; `StopAndManual` only | 1 new column (order) |
| 4 | Rollout strategy & health | `Strategy` enum as metadata; synchronous only; `HealthChecking` reserved | 1 new column, 1 enum extension |
| 5 | Promotion / gates / freeze | Implicit promotion by `PromotionRank`; soft enforcement with overrides; freeze windows table | 1 new table, 2 new columns |
| 6 | AuthZ & SoD | External IAM owns AuthZ; 4-eyes is workflow logic | None |
| 7 | Artifact provenance | Six provenance columns on `Release`; quarantine reason via status-change history | 1 new table, 6 new columns |
| 8 | Topology & tenancy | Multi-region via existing `DeploymentTarget.Region`; multi-target cascade reuses parent/child; tenancy deferred | None (query/index updates only) |

**Total new tables:** 4 (`ConfigurationSettingHistory`, `EnvironmentFreezeWindow`, `ReleaseStatusChange`, `DeploymentSecretBinding`).
**Total new columns:** 11 (across `Deployment`, `Release`, `ApplicationService`).
**Total new enum values:** 1 (`Deployment.Status.HealthChecking` — reserved, not written by v1).

---

## 3. Section 7 #1 — Current-pin fallback

**Decision:** When a `ReleaseComposition` entry has `PinMode = Current` and the target service has never been deployed successfully in the target environment, fall back to the global-catalog **Latest** (newest `Release` with `Status = Available`). If no `Available` releases exist for the service at all, the deployment fails before any work starts with a clear "service has no available releases" error.

**`Latest` scope:** global catalog. `Release.Status` is the safety net — `Quarantined` and `Superseded` releases are excluded from the candidate set. (Per Section 7 #7, status transitions are auditable via `ReleaseStatusChange`.)

**Resolution order (Application layer, `Features/Releases/ResolveCompositionPins/`):**
1. `Pinned` → use `ServiceReleaseId` directly (no resolution needed).
2. `Latest` → newest `Release.SemanticVersion` for the service where `Status = 'Available'`.
3. `Current` → most recent `Deployment.ReleaseId` for the service where `EnvironmentId = target` and `Status = 'Succeeded'`. If none, recurse to step 2.

**Audit:** when fallback fires, emit a `DeploymentEvent`:

```
EventType = 'CurrentPinFallbackApplied'
Detail    = { ServiceId, ResolvedReleaseId, Reason: 'NeverDeployedInEnvironment' }
```

Operators can spot fallback events in the deployment audit stream — useful for catching "this `Current` pin was always meant to be a real version" mistakes.

**Schema delta:** none. Pure Application-layer logic + an existing-table event row.

---

## 4. Section 7 #2 — Configuration precedence

### 4.1 Resolution ladder (Rule X)

For any configuration `Key` and a deploy of (Service S, in the context of Application A, into Environment E), the effective value is resolved in this order. The first matching row wins:

| Priority | Owner | Scope | Match condition |
| --- | --- | --- | --- |
| 1 (highest) | Application A | env-specific | `DeployableUnitId = A` AND `EnvironmentId = E` AND `Key = K` |
| 2 | Service S | env-specific | `DeployableUnitId = S` AND `EnvironmentId = E` AND `Key = K` |
| 3 | Application A | unit-default | `DeployableUnitId = A` AND `EnvironmentId IS NULL` AND `Key = K` |
| 4 (lowest) | Service S | unit-default | `DeployableUnitId = S` AND `EnvironmentId IS NULL` AND `Key = K` |

Env always trumps unit-default within the same owner-tier; App always trumps Service within the same env-tier.

**Origin tracking:** the resolver returns `(Key → Value/SecretReference, Origin)` where `Origin` is one of the four labels above. Surfaces as a "why is this value what it is?" affordance in the UI.

### 4.2 Versioned history

Every change to a `ConfigurationSetting` row raises a domain event:

```csharp
ConfigurationSettingChanged(
    ConfigurationSettingId,
    OldValue, OldSecretReference, OldIsSecret,
    NewValue, NewSecretReference, NewIsSecret,
    ChangeKind: Created | Updated | Deleted,
    ChangedByPrincipal,
    ChangedAtUtc)
```

An Infrastructure handler projects to:

```
ConfigurationSettingHistory (new table)
  HistoryId               PK
  ConfigurationSettingId  FK → ConfigurationSetting
  ChangeKind              enum: Created | Updated | Deleted
  OldValue                string NULL
  OldSecretReference      string NULL
  OldIsSecret             bool   NULL
  NewValue                string NULL
  NewSecretReference      string NULL
  NewIsSecret             bool   NULL
  ChangedByPrincipal      string
  ChangedAtUtc            datetime
  -- index on (ConfigurationSettingId, ChangedAtUtc DESC)
```

Supports "config as of time T" queries via `WHERE ChangedAtUtc <= @T ORDER BY ChangedAtUtc DESC LIMIT 1` per `(ConfigurationSettingId)`.

### 4.3 Secret-version snapshot per deployment

When child `Deployment` rows are created, the Application layer resolves every secret reference in scope for that deployment to its current Key Vault **versioned** URI (e.g., `https://vault.vault.azure.net/secrets/foo/abc123def456`) and writes one row per resolution:

```
DeploymentSecretBinding (new entity, lives inside Deployment aggregate)
  DeploymentId            FK → Deployment      (parent)
  ConfigurationSettingId  FK → ConfigurationSetting
  ResolvedSecretUri       string               -- versioned KV URI
  ResolvedAtUtc           datetime
  PK = (DeploymentId, ConfigurationSettingId)
```

**Binding scope:** *all resolvable secret references for the deployed unit, including app-level scope* (the bundle-overrides-service intuition from 4.1). Bindings are immutable once written — they survive secret rotation and let incident response answer "what secret version was this deployment using at time T?"

**Schema delta:** 1 new table (`ConfigurationSettingHistory`), 1 new entity (`DeploymentSecretBinding`).

---

## 5. Section 7 #3 — Ordering, compatibility, failure semantics

### 5.1 Cascade ordering

Ordering lives **at the catalog level**:

```
ApplicationService (extended)
  + DeploymentOrder  int  DEFAULT 0    -- lower runs first; ties run in parallel
```

Catalog-level: the structural fact "DB migration always runs before API" is set once on `ApplicationService` and applies to every release that bundles those services. Per-release reordering is **not supported in v1** — an Application that needs a one-off reorder must change the catalog membership ordering or add the reorder logic to the release pipeline.

Same-priority entries run in parallel within the cascade.

### 5.2 Inter-service compatibility

**No model-side compatibility rules in v1.** The `ReleaseComposition` BOM itself is the compatibility contract — the engineer (or CI) who built the release composition certified that the pinned/floating service versions work together. Adding a `ServiceCompatibilityRule` aggregate is deferred.

### 5.3 Failure semantics

**`StopAndManual` is the only behavior in v1.** When a child deployment in a cascade fails:

1. The failing child's `Status` becomes `Failed`.
2. The parent `Deployment.Status` becomes `Failed`.
3. Remaining `Queued` children stay `Queued` indefinitely (visible in UI; operator triages).
4. No automatic rollback is attempted.

A future `RolloutPolicy` column on `Release` (Application releases) will let teams opt in to `AutoRollback` or `ContinueIgnore`, defaulting to `StopAndManual`. Deferred to v2.

**Schema delta:** 1 new column (`ApplicationService.DeploymentOrder`).

---

## 6. Section 7 #4 — Rollout strategy & health verification

### 6.1 Strategy is metadata only

The model **tracks** the strategy that was chosen and the health outcome observed. The model does **not** orchestrate canary mechanics, blue/green swap, or traffic shifts — those are deployment-target concerns (Kubernetes, App Service slot swap, Container Apps revisions) which already have native primitives.

```
Deployment (extended)
  + Strategy   enum  DEFAULT 'Direct'    -- Direct | BlueGreen | Canary | Rolling
```

Strategy affects audit/UI/reporting only. A canary deployment still goes `Queued → Running → Succeeded/Failed` like any other; the "running" state might just last hours during canary observation.

### 6.2 Health verification — synchronous for v1

The deployment runner does its thing, runs smoke tests, reports `Succeeded` or `Failed` in one shot. State transitions are atomic.

**The `HealthChecking` state is reserved in the enum** but never written by v1 code. When asynchronous health verification lands (deployment reports `Deployed-AwaitingHealth`, external observer flips to terminal), this state is already in the type — no migration needed at that point.

```
Deployment.Status enum (extended)
  Queued | Running | Succeeded | Failed | RolledBack | Cancelled
  + (HealthChecking — reserved for v2; not written by v1)
```

### 6.3 State machine shape

```
                              ┌──── Cancelled (operator action, before Running)
                              │
Queued ──► Running ──► Succeeded ──► RolledBack    (set when a fresh rollback deployment succeeds;
   │          │                                    original row's Status flips to RolledBack)
   │          ▼
   │       Failed
   │
   └──► Cancelled    (queue cleanup)
```

**`RolledBack` semantics:** when an operator rolls back, a *new* `Deployment` row is created with its own `ReleaseId` (the earlier release being rolled back to). On that new row's `Succeeded`, the **original** `Deployment.Status` is updated from `Succeeded` to `RolledBack`. The original row is never otherwise mutated; this is the only allowed transition out of `Succeeded`. Maintains the "every row is immutable history except for `Status`" invariant.

**Smoke test outcomes** (sync or async) are written as `DeploymentEvent` rows (`SmokeTestRun`, `SmokeTestPassed`, `SmokeTestFailed`) — no new columns on `Deployment`.

**Schema delta:** 1 new column (`Deployment.Strategy`), 1 reserved enum value (`HealthChecking`).

---

## 7. Section 7 #5 — Promotion, gates, freeze windows

### 7.1 Promotion path — implicit by `PromotionRank`

The existing `Environment.PromotionRank int` column drives the promotion path. Promote in ascending order: Dev (1) → Test (2) → Staging (3) → Prod (4). Hotfix paths that skip tiers are supported via the soft-override below — no separate `PromotionPath` table in v1.

### 7.2 Enforcement — soft-policed

The Application layer's `StartDeployment` handler checks: "is there a successful `Deployment` of the same `ReleaseId` in the environment one rank lower than this target?" If yes, proceed. If no, require:

```
Deployment (extended)
  + SkipPromotionPathReason  string  NULL    -- non-null when an out-of-order promotion is deliberate
```

Empty `SkipPromotionPathReason` + no prior-environment success = reject. Non-empty = proceed with the reason captured in the immutable history.

### 7.3 Gates beyond approval — none in v1

Only the existing `Approval` entity. Polymorphic `PromotionGate` (Health gates, Time gates, External gates) is deferred. The simplest gate richness comes from setting `Environment.RequiresApproval = true` per-env and using the existing `Approval` row mechanism.

**Optional refinement (worth adding when needed, not v1):**

```
Environment
  + MinDistinctApprovers  int  DEFAULT 1    -- raise to 2 for Prod to require 2 distinct Approval rows
```

Skipped for v1 until a team explicitly wants N-eyes per environment.

### 7.4 Freeze windows

```
EnvironmentFreezeWindow (new table)
  FreezeWindowId      PK
  EnvironmentId       FK → Environment
  StartUtc            datetime
  EndUtc              datetime
  Reason              string             -- e.g. "Holiday freeze 2026 EOY"
  CreatedByPrincipal  string
  CreatedAtUtc        datetime
  -- index on (EnvironmentId, StartUtc, EndUtc)
```

**No recurrence in v1.** A "every Friday 4pm – Monday 8am" policy generates explicit rows for the next N months via a maintenance job. Keeps the model relational; no cron/rrule parser.

**Scope = environment only.** A future "freeze for Service X only" would add a nullable `DeployableUnitId` column.

**Enforcement = soft-override:**

```
Deployment (extended)
  + OverrideFreezeReason     string  NULL    -- non-null when deployed during a freeze window
```

`StartDeployment` handler checks: "is `StartedAtUtc` inside any window for this environment?" If yes, require `OverrideFreezeReason` non-empty. Audit captures the reason.

**Schema delta:** 1 new table (`EnvironmentFreezeWindow`), 2 new columns (`Deployment.SkipPromotionPathReason`, `Deployment.OverrideFreezeReason`).

---

## 8. Section 7 #6 — Authorization & segregation of duties

### 8.1 AuthZ — fully external

The deployment model captures **who did what**, not **who can do what**. External IAM (Entra ID groups / claims policies) owns the authorization graph. The API layer enforces policies before calling Application handlers; the handlers themselves trust whoever they're called by.

**Audit trail is complete via existing fields:**
- `Deployment.TriggeredByPrincipal` — who initiated.
- `Deployment.TriggeredBy` — how (Manual / Pipeline / AutoPromote).
- `Approval.ApproverPrincipal` — who approved.
- `ConfigurationSettingHistory.ChangedByPrincipal` — who changed config.
- `Release.PublishedByPrincipal` — who/what published the release.

**No `Role`, `Permission`, `RoleAssignment`, or `EnvironmentAccess` tables in v1.** If teams want a UI to *see* who can deploy what (without logging into Entra ID), introduce a thin `EnvironmentAccess` projection later as a read model — one new aggregate, no migration of existing data.

### 8.2 Segregation of duties — workflow rule

4-eyes principle enforced in the Application layer's `ApproveDeployment` handler:

```
Approval.Status = 'Approved' is only accepted when
    Approval.ApproverPrincipal != Deployment.TriggeredByPrincipal
    (over a candidate Approval row for the same DeploymentId)
```

Not a CHECK constraint — the `Approval` table is append-only and the verdict is computed by the handler. Captures naturally in audit since both principals are recorded.

**Schema delta:** none.

---

## 9. Section 7 #7 — Artifact provenance & supply chain

### 9.1 Provenance pointers on `Release`

Six new nullable columns. Populated by the publish pipeline going forward; nullable for migration safety on any pre-existing rows.

```
Release (extended)
  + ArtifactSha256             string  NULL    -- integrity checksum at publish time
  + SbomUri                    string  NULL    -- bom-vex.json in Nexus raw repo
  + VulnerabilityReportUri     string  NULL    -- vulnerabilities.json in the same repo
  + CiRunUrl                   string  NULL    -- clickable: http://jenkins:8080/job/cicd-build/42/
  + CiRunId                    string  NULL    -- programmatic: "cicd-build/#42"
  + PublishedByPrincipal       string  NULL    -- build/publish identity (often a system account)
```

The existing `Release.CommitSha` + `Release.BuildNumber` already cover "what code, what build #." These six add "what's the hash, where can I read about it, who published it."

**Deployment-time verification** is workflow logic: pull the artifact, re-hash, compare to `Release.ArtifactSha256`, abort on mismatch. No new column on `Deployment`.

### 9.2 Quarantine reason — via status-change history

`Release.Status` already supports `Quarantined`, but the current schema captures nothing about *why*. Use a domain-event audit trail to retain transition history (a Release can be `Available → Quarantined → Available → Quarantined` repeatedly as new CVEs surface):

```
ReleaseStatusChange (new table)
  ChangeId            PK
  ReleaseId           FK → Release
  FromStatus          enum
  ToStatus            enum
  Reason              string NULL
  ChangedByPrincipal  string
  ChangedAtUtc        datetime
  -- index on (ReleaseId, ChangedAtUtc DESC)
```

Captures `Available → Quarantined` (CVE found), `Quarantined → Available` (CVE patched in a downstream re-release), and `Available → Superseded` (newer release published). Operators can see the full timeline.

### 9.3 Vulnerability findings — not stored inline

`Release.VulnerabilityReportUri` points to the existing `vulnerabilities.json` Trivy produces. The deployment model does **not** store vulnerability data inline. If a fast query like "Available releases with no Critical CVEs" becomes valuable, that's a materialized read-model projection built by periodically reading the JSON files — future work, not v1.

### 9.4 Signing / attestation — deferred

SLSA / Sigstore / Cosign / in-toto attestation lands as a separate aggregate (`ReleaseAttestation` — one `Release` → many attestations from different signers) when there's a concrete use case. Modeling cryptographic provenance without one means modeling speculative semantics.

**Schema delta:** 6 new columns on `Release`, 1 new table (`ReleaseStatusChange`).

---

## 10. Section 7 #8 — Environment topology & tenancy

### 10.1 Multi-region — existing schema

`DeploymentTarget` already carries `EnvironmentId` and `Region`. A single `Environment` (e.g., Prod) with multiple `DeploymentTarget` rows tagged by region is the v1 shape.

If regions ever need to gate independently (deploy to EU first, then US), promote them to separate `Environment` rows with their own `PromotionRank`. Either shape works without schema changes.

### 10.2 Multi-target cascade — refined parent/child convention

The existing `Deployment.ParentDeploymentId` self-reference handles multi-target rollouts. A standalone service-to-multiple-targets deploy creates **one parent `Deployment`** (null `TargetId`, null `ParentDeploymentId`) plus **N child `Deployment` rows** (one per target, each with the same `ServiceId` and `ReleaseId`).

Cascade shape for "deploy Application A bundling Services A + B to environment E with two targets each":

```
Application Deployment        (parent, ReleaseId = A's app release, TargetId = NULL)
├── Service A → Target1       (child, ReleaseId = A's resolved service release, TargetId = T1)
├── Service A → Target2       (child, same ReleaseId, TargetId = T2)
├── Service B → Target1       (child, ReleaseId = B's resolved service release, TargetId = T1)
└── Service B → Target2       (child, same ReleaseId, TargetId = T2)
```

Invariant: **a `Deployment` row with `TargetId IS NULL` is always a logical parent**; concrete deploys always live at the leaves with a non-null `TargetId`.

### 10.3 Tenancy — deferred to v2

Out of scope for v1. If/when SaaS multi-tenancy lands:
- New `Tenant` aggregate.
- Optional `Deployment.TenantId` (deploy to a single tenant on a shared cluster vs. all tenants).
- Optional `ConfigurationSetting.TenantId` (tenant-specific overrides — adds a fifth tier to the resolution ladder in §4.1).

No skeleton in v1 — modeling tenancy without a concrete use case means modeling the wrong abstraction. All future tenancy additions are nullable columns and a new aggregate; no breaking changes to existing queries.

### 10.4 Query impact — Q1 becomes Q1′

The handoff's Q1 ("effective running versions of every service composing an application, in one environment") becomes per-target in a multi-target world. The new primitive:

```sql
-- Q1' — effective running version per (service, target) in an environment
SELECT s.Name AS Service, dt.ResourceId AS Target, dt.Region,
       rel.SemanticVersion, latest.CompletedAtUtc
FROM ApplicationService aps
JOIN Service s ON s.ServiceId = aps.ServiceId
JOIN DeploymentTarget dt ON dt.EnvironmentId = @EnvironmentId
CROSS APPLY (
    SELECT TOP 1 d.ReleaseId, d.CompletedAtUtc
    FROM Deployment d
    JOIN Release r ON r.ReleaseId = d.ReleaseId
    WHERE r.DeployableUnitId = s.ServiceId
      AND d.TargetId         = dt.TargetId
      AND d.Status           = 'Succeeded'
    ORDER BY d.CompletedAtUtc DESC
) latest
JOIN Release rel ON rel.ReleaseId = latest.ReleaseId
WHERE aps.ApplicationId = @ApplicationId;
```

The original env-level Q1 becomes a **derived view** over Q1′: collapse per-service if all targets agree, otherwise tag as `Mixed (N versions)`. Computed in the read layer / UI; not a stored thing.

**Index updates:**
- Add: `Deployment(TargetId, Status, CompletedAtUtc)` — supports Q1′.
- Keep: `Deployment(EnvironmentId, Status, CompletedAtUtc)` — supports env-level rollup queries and the existing per-environment Q1 for non-multi-target environments.

**Schema delta:** none. Cascade convention + query and index updates only.

---

## 11. Consolidated schema deltas (one place to look)

### New tables

| Table | Purpose | Aggregate |
| --- | --- | --- |
| `ConfigurationSettingHistory` | Versioned audit trail of config changes | `Configuration` (projection) |
| `EnvironmentFreezeWindow` | No-deploy time windows per environment | `Environments` |
| `ReleaseStatusChange` | Timeline of `Release.Status` transitions with reasons | `Releases` (projection) |
| `DeploymentSecretBinding` | Per-deployment snapshot of resolved versioned Key Vault URIs | `Deployments` (entity) |

### New columns on existing tables

| Table | Column | Type | Purpose |
| --- | --- | --- | --- |
| `ApplicationService` | `DeploymentOrder` | int DEFAULT 0 | Cascade ordering (§7.1) |
| `Deployment` | `Strategy` | enum DEFAULT 'Direct' | Metadata-only rollout strategy (§6.1) |
| `Deployment` | `SkipPromotionPathReason` | string NULL | Out-of-order promotion override reason (§7.2) |
| `Deployment` | `OverrideFreezeReason` | string NULL | Deploy-during-freeze override reason (§7.4) |
| `Release` | `ArtifactSha256` | string NULL | Provenance: integrity checksum (§9.1) |
| `Release` | `SbomUri` | string NULL | Provenance: link to SBOM (§9.1) |
| `Release` | `VulnerabilityReportUri` | string NULL | Provenance: link to vulnerabilities.json (§9.1) |
| `Release` | `CiRunUrl` | string NULL | Provenance: clickable CI run URL (§9.1) |
| `Release` | `CiRunId` | string NULL | Provenance: programmatic CI run key (§9.1) |
| `Release` | `PublishedByPrincipal` | string NULL | Provenance: build/publish identity (§9.1) |

### New / reserved enum values

| Enum | Existing | Added in v1 | Reserved for v2 |
| --- | --- | --- | --- |
| `Deployment.Status` | Queued, Running, Succeeded, Failed, RolledBack, Cancelled | — | `HealthChecking` |
| `Deployment.Strategy` | (new column) | Direct, BlueGreen, Canary, Rolling | — |

### New indexes

| Table | Index |
| --- | --- |
| `Deployment` | `(TargetId, Status, CompletedAtUtc)` — supports Q1′ (§10.4) |
| `ConfigurationSettingHistory` | `(ConfigurationSettingId, ChangedAtUtc DESC)` |
| `ReleaseStatusChange` | `(ReleaseId, ChangedAtUtc DESC)` |
| `EnvironmentFreezeWindow` | `(EnvironmentId, StartUtc, EndUtc)` |

Existing indexes from Section 8 of the handoff remain in place.

### CHECK constraints (unchanged from the handoff)

- `ReleaseComposition` pin-mode invariant: `(PinMode = 'Pinned' AND ServiceReleaseId IS NOT NULL) OR (PinMode IN ('Latest','Current') AND ServiceReleaseId IS NULL)`.
- Unique on `Release(DeployableUnitId, SemanticVersion)`.
- Composite PKs on `ApplicationService(ApplicationId, ServiceId)` and `ReleaseComposition(ApplicationReleaseId, ServiceId)`.

---

## 12. Deferred to v2 (will not be implemented now)

These are explicitly out of scope for v1 and have been called out in their respective sections. Most extend the model additively (new column or new aggregate) without breaking changes.

| Deferred item | Section | Trigger for v2 implementation |
| --- | --- | --- |
| Per-release `RolloutPolicy` (`AutoRollback` / `ContinueIgnore`) | 5.3 | Team wants opt-in auto-rollback semantics |
| Polymorphic `PromotionGate` (Health / Time / External gates) | 7.3 | Need machine-enforced gates beyond manual approval |
| `Environment.MinDistinctApprovers` | 7.3 | Team wants N-eyes per-environment beyond default 1 |
| Asynchronous health verification (`HealthChecking` state written) | 6.2 | Canary observation windows become long enough to warrant async |
| `ServiceCompatibilityRule` aggregate | 5.2 | Services start breaking each other across versions |
| `EnvironmentFreezeWindow.DeployableUnitId` scope narrowing | 7.4 | Need per-unit freezes (e.g. mobile-app store freeze) |
| Cron / rrule recurrence for freeze windows | 7.4 | Generating windows ahead becomes a maintenance burden |
| `ReleaseAttestation` aggregate (Sigstore / SLSA / Cosign) | 9.4 | Supply-chain attestation requirements |
| Inline `CriticalVulnCount` projection on `Release` | 9.3 | Need fast "show me clean releases" queries |
| `EnvironmentAccess` projection (UI view of who can deploy what) | 8.1 | Team wants in-model AuthZ visibility |
| `Tenant` aggregate + tenant scope on `Deployment` and `ConfigurationSetting` | 10.3 | Real SaaS multi-tenant requirements |
| Explicit `PromotionPath(From, To)` table | 7.1 | Hotfix-path frequency justifies first-class edges |
| Per-release `DeploymentOrder` (vs catalog-level) | 5.1 | Releases routinely need to reorder catalog deployment order |

---

## 13. What happens next

The implementation work order follows the stack confirmation in handoff Section 8.

**Confirmed v1 stack** (from the project setup phase):
- .NET 10 / C# 13 / EF Core 10
- SQL Server (provider-agnostic via `UseSqlServer` in `Deployment.Infrastructure`)
- Wolverine 6.4.3 as the CQRS dispatcher
- xUnit + FluentAssertions for tests
- Shared-PK 1:1 mapping for the `DeployableUnit` / `Service` / `Application` split (Service and Application kept as separate aggregates)
- Domain events dispatched in-process via Wolverine after `SaveChangesAsync`

**Suggested implementation order** (each step is a separate PR-sized chunk):

1. **Catalog aggregates** — `DeployableUnit`, `Service`, `Application`, `ApplicationService`. Just identity + membership + version-agnostic relationships. Two aggregates, one join entity.
2. **Release aggregate** — `Release` + `ReleaseComposition`. Pin-mode invariant enforced inside the aggregate.
3. **Environment + DeploymentTarget aggregate** — small, mostly reference data.
4. **Configuration aggregate** — `ConfigurationSetting` + the change event. Add `ConfigurationSettingHistory` projection.
5. **Deployment aggregate** — `Deployment`, `Approval`, `DeploymentEvent`, `DeploymentSecretBinding`. The most complex aggregate; lands last.
6. **Resolution features** in `Deployment.Application/Features/`:
   - `ResolveCompositionPins` (§3)
   - `ResolveEffectiveConfig` (§4.1)
   - `StartDeployment` (cascade + ordering + promotion check + freeze check + secret binding snapshot)
   - `ApproveDeployment` (4-eyes check)
   - `GetEffectiveVersions` (Q1′)
   - `GetDeploymentBaseline` (Q2)
   - `ChangeReleaseStatus` (with `ReleaseStatusChange` history)

7. **Initial EF Core migration** — single migration creating all tables (since none exist yet).
8. **Test coverage** — domain unit tests per aggregate; application tests with fake repositories; integration tests for the resolution features.

Each step's PR should not introduce code for any item from the "Deferred to v2" list in Section 12.
