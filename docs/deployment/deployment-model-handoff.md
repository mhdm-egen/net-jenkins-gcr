# Design Handoff: .NET Application Deployment Data Model

You are a senior .NET developer and DevOps engineer. You are joining an in-progress
design effort. **You have no prior context other than this document — treat it as the
source of truth.** Read it fully, then follow the instructions in "Your task" at the end.

---

## 1. Goal

Design (and eventually implement) a data model for managing the deployment of .NET
applications into multiple environments. The model must track *what* gets deployed,
*which version*, *where*, *when*, *by whom*, and *with what configuration* — and must
support composite applications made of independently deployable services.

## 2. Governing principles

- **Build once, deploy many.** A `Release` is an immutable, environment-neutral artifact.
  The same artifact is promoted Dev → Test → Staging → Prod. Nothing environment-specific
  is ever baked into it.
- **Configuration is separate from the artifact.** Per-environment values live in the model
  (mirroring .NET's `IConfiguration` layering: `appsettings.json` →
  `appsettings.{Environment}.json` → env vars → Key Vault; `ASPNETCORE_ENVIRONMENT` selects
  the environment).
- **Secrets are references, not values.** Store a Key Vault URI / secret name; resolve at
  runtime via managed identity. Never store secret values.
- **Rollback is just another deployment** to an earlier (retained, immutable) release.

## 3. Core concepts

- **DeployableUnit** — supertype for anything that is versioned and independently deployable.
  Two subtypes share its identity (shared primary key):
  - **Service** — an actual runnable .NET unit (Web API, worker, function, etc.).
  - **Application** — a *composition* of one or more services. It is also versioned and
    deployable in its own right, but it primarily acts as a bill-of-materials / manifest.
- A **Service can belong to one or more Applications** (many-to-many; e.g. a shared identity
  service used by several apps).
- Both Services and Applications have **Releases**, each with its **own independent version
  sequence**. An Application's release version need not relate to its services' versions —
  an Application release can have a higher/newer version than the service releases it bundles.
- An **Application release is a bill of materials (BOM)**: it lists which service versions
  compose it. Each entry has a **pin mode**:
  - `Pinned` — an explicit service release (deterministic, immutable).
  - `Latest` — resolve to the newest release of that service in the catalog at deploy time.
  - `Current` — resolve to whatever release of that service is **already deployed in the
    target environment** at deploy time (environment-relative).
- **Consequence of floating pins:** an Application release with `Latest`/`Current` entries is
  **not fully immutable** — it resolves differently per environment/time. The immutable
  historical truth therefore lives in the **deployment records**, not the manifest. The
  manifest is the recipe; the cascade of child `Deployment` rows is what was actually deployed.
- **Declared baseline vs. effective state.** Deploying an Application produces a *declared,
  tested baseline*. But because any unit can be deployed independently (e.g. a service hotfix),
  the *effective* state of an environment is whatever the latest successful per-service
  deployment says — which can drift from any application baseline. The model must answer both
  "what baseline did we promote here?" and "what is actually running right now?".

## 4. Entities and attributes

Illustrative types use `guid` PKs and string enums — propose alternatives if you prefer.

### DeployableUnit (supertype)
- `DeployableUnitId` (PK)
- `UnitType` — discriminator: `Service` | `Application`
- `Name` (unique)
- `IsActive`
- `CreatedAtUtc`

### Service (subtype; PK = DeployableUnitId)
- `ServiceId` (PK, also FK → DeployableUnit)
- `Kind` — `WebApi` | `Mvc` | `WorkerService` | `AzureFunction` | `Console` | ...
- `RepositoryUrl`
- `TargetFramework` — e.g. `net8.0`

### Application (subtype; PK = DeployableUnitId)
- `ApplicationId` (PK, also FK → DeployableUnit)
- `Description`

### ApplicationService (catalog membership; many-to-many)
- `ApplicationId` (FK → Application)
- `ServiceId` (FK → Service)
- `Role`
- `IsOptional`
- PK = (`ApplicationId`, `ServiceId`)
- This is the version-agnostic "Application X is made of Services A, B, C." It is distinct
  from the BOM (which is version-specific, per release).

### Release (a version of any DeployableUnit)
- `ReleaseId` (PK)
- `DeployableUnitId` (FK → DeployableUnit)
- `SemanticVersion`
- `BuildNumber`
- `CommitSha`
- `ArtifactType` — `Zip` | `ContainerImage` | `NuGet`
- `ArtifactUri` (**nullable** — null for a manifest-only Application release)
- `CreatedAtUtc`
- `Status` — `Available` | `Superseded` | `Quarantined`
- Unique on (`DeployableUnitId`, `SemanticVersion`)

### ReleaseComposition (the BOM — entries of an Application release)
- `ApplicationReleaseId` (FK → Release; must reference a release of an *Application* unit)
- `ServiceId` (FK → Service)
- `PinMode` — `Pinned` | `Latest` | `Current`
- `ServiceReleaseId` (**nullable** FK → Release; set **only** when `PinMode = Pinned`; must
  reference a release of that same Service)
- PK = (`ApplicationReleaseId`, `ServiceId`)
- Invariant (enforce with a CHECK constraint):
  `(PinMode = 'Pinned' AND ServiceReleaseId IS NOT NULL)
   OR (PinMode IN ('Latest','Current') AND ServiceReleaseId IS NULL)`

### Environment
- `EnvironmentId` (PK)
- `Name` (unique) — e.g. Development, Test, Staging, Production
- `PromotionRank` (int; orders the promotion path)
- `RequiresApproval`
- `IsProduction`

### DeploymentTarget (where a service physically runs within an environment)
- `TargetId` (PK)
- `EnvironmentId` (FK → Environment)
- `TargetKind` — `IIS` | `AzureAppService` | `KubernetesCluster` | `ContainerApp` | `VM`
- `ResourceId`
- `Region`
- `Slot` (optional)

### Deployment (deploys a Release into an Environment)
- `DeploymentId` (PK)
- `ReleaseId` (FK → Release; service or application)
- `EnvironmentId` (FK → Environment)
- `TargetId` (**nullable** FK → DeploymentTarget; an app-level cascade *parent* may be null;
  its child rows carry the concrete targets)
- `ParentDeploymentId` (**nullable** self-FK → Deployment; groups a cascade)
- `Status` — `Queued` | `Running` | `Succeeded` | `Failed` | `RolledBack` | `Cancelled`
- `Trigger` — `Manual` | `Pipeline` | `AutoPromote`
- `TriggeredBy`
- `StartedAtUtc`
- `CompletedAtUtc`
- **Cascade model:** deploying an Application release creates one parent `Deployment` plus one
  child `Deployment` per service (each with its own resolved `ReleaseId` and `TargetId`,
  pointing back via `ParentDeploymentId`). A standalone service deploy has a null parent. The
  set of child deployments is the immutable record of what an application rollout installed.

### ConfigurationSetting (per-unit, per-environment config)
- `ConfigurationSettingId` (PK)
- `DeployableUnitId` (FK → DeployableUnit; service or application)
- `EnvironmentId` (**nullable** FK → Environment; null = unit-wide default across environments)
- `Key` — hierarchical, e.g. `ConnectionStrings:Default`, `FeatureFlags:NewCheckout`
- `Value` (nullable when secret)
- `IsSecret`
- `SecretReference` — Key Vault URI / secret name (store the reference, never the value)
- `ValueType` — `String` | `Int` | `Bool` | `Json`
- Resolution precedence is a deliberate decision (see open items): typically environment
  override beats unit default; if application-level and service-level config both exist, the
  ordering between them must be defined.

### Approval (gate on a Deployment; unchanged from earlier design)
- `ApprovalId` (PK), `DeploymentId` (FK), `ApproverPrincipal`,
  `Status` (`Pending`|`Approved`|`Rejected`), `DecidedAtUtc`, `Comment`

### DeploymentEvent (audit/history of a Deployment)
- `EventId` (PK), `DeploymentId` (FK), `Timestamp`, `EventType`, `Detail`

## 5. Relationships (cardinalities)

- DeployableUnit 1—1 Service (subtype)
- DeployableUnit 1—1 Application (subtype)
- DeployableUnit 1—* Release
- DeployableUnit 1—* ConfigurationSetting
- Application *—* Service (via ApplicationService)
- Release (Application) 1—* ReleaseComposition  (manifest entries)
- Service 1—* ReleaseComposition  (each entry names a service)
- Release (Service) 0/1—* ReleaseComposition  (optional pinned version)
- Release 1—* Deployment
- Environment 1—* Deployment
- Environment 1—* DeploymentTarget
- Environment 1—* ConfigurationSetting
- DeploymentTarget 1—* Deployment
- Deployment 1—* Deployment (self-ref: parent cascades to children)
- Deployment 1—* Approval
- Deployment 1—* DeploymentEvent

## 6. Required queries (acceptance criteria for the model)

These must be answerable cleanly. Treat them as tests of the schema.

**Q1 — Effective running versions of every service composing an application, in one
environment** (reflects reality, including drift from independent hotfixes). For each member
service, take its most recent *successful* deployment in the target environment:

```sql
SELECT s.Name AS Service, rel.SemanticVersion, latest.CompletedAtUtc
FROM ApplicationService aps
JOIN Service s ON s.ServiceId = aps.ServiceId
CROSS APPLY (
    SELECT TOP 1 d.ReleaseId, d.CompletedAtUtc
    FROM Deployment d
    JOIN Release r ON r.ReleaseId = d.ReleaseId
    WHERE r.DeployableUnitId = s.ServiceId
      AND d.EnvironmentId    = @EnvironmentId
      AND d.Status           = 'Succeeded'
    ORDER BY d.CompletedAtUtc DESC
) latest
JOIN Release rel ON rel.ReleaseId = latest.ReleaseId
WHERE aps.ApplicationId = @ApplicationId;
```

**Q2 — What a specific application deployment installed** (immutable record of one rollout):
select child `Deployment` rows `WHERE ParentDeploymentId = @AppDeploymentId` and join to
`Release` / `Service`.

Q1 and Q2 will diverge exactly when a service was deployed independently of the app baseline.

## 7. Open design dimensions (NOT yet decided — do not assume)

These are intentionally unresolved. Help the user reason through them before/while modeling.
Flag where each would add tables or columns.

1. **Current-pin fallback rule.** When a `Current` BOM entry references a service that has
   never been deployed to the target environment, what happens? (Fall back to `Latest`, or
   fail the deployment with a clear error?) Decide rather than discover at runtime.
2. **Config precedence** when both application-level and service-level settings exist; and
   whether config changes should be versioned/audited as first-class objects; secret rotation.
3. **Deployment ordering & inter-service dependencies** within a cascade ("migrate before
   API"; "Service A requires Service B ≥ 3.0"). The BOM lists *what*, not *order* or
   *compatibility*.
4. **Rollout strategy & health verification** — canary / blue-green / rolling, smoke tests,
   auto-rollback triggers (the `Deployment` status state machine fleshed out).
5. **Promotion rules, gates, freeze windows** — which environment promotes to which, blackout
   periods, conditions beyond manual approval.
6. **Authorization & segregation of duties** — who can deploy/approve what, where; stricter
   rules for production.
7. **Artifact provenance / supply chain** — registry/feed location, checksums, signing, SBOM,
   link back to the CI run.
8. **Environment topology & tenancy** — multi-region, multiple targets per service per
   environment, per-tenant deployment matrix.

## 8. Proposed implementation stack (confirm before coding)

- Latest .NET LTS, C#, EF Core (Code-First).
- Database provider: assume SQL Server unless told otherwise — confirm.
- **DeployableUnit/Service/Application** → table-per-type (TPT) inheritance, OR keep as
  explicit 1:1 tables sharing a PK if you prefer to avoid EF inheritance; discuss the
  trade-off.
- Fluent-config spots that need care:
  - TPT (or shared-PK 1:1) mapping for the supertype/subtypes.
  - `ReleaseComposition`: two FKs to `Release` (mandatory app side, optional pinned side) plus
    an FK to `Service` — use `DeleteBehavior.Restrict` to avoid multiple cascade paths.
  - `Deployment.ParentDeploymentId` self-reference — `Restrict`.
  - Composite keys on `ApplicationService` and `ReleaseComposition`.
  - CHECK constraint enforcing the `ReleaseComposition` pin-mode invariant.
  - Unique index on (`DeployableUnitId`, `SemanticVersion`) in `Release`.
  - Enums stored as strings (or lookup tables) — pick one and be consistent.
  - Indexes supporting Q1: `Deployment(EnvironmentId, Status, CompletedAtUtc)` and
    `Release(DeployableUnitId)`.

## 9. Your task

1. Restate the model in your own words and **surface any ambiguities or contradictions** you
   find before writing anything.
2. Help the user work through the **open design dimensions in Section 7**. That is the current
   focus.
3. **Do NOT generate EF Core entities, a DbContext, migrations, or SQL DDL until the user
   explicitly asks.** Implementation is intentionally deferred until the open items settle.
4. When asked to implement, follow Section 8 and confirm the stack first.
