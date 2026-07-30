# AI features

The platform has an AI layer built on the official Anthropic SDK, plus a **metering service** that
turns every model call into a rated, queryable cost ledger. Both are optional: with no API key the
app runs exactly as before, AI actions simply don't appear.

What ships today:

| Feature | Where | What it does |
| --- | --- | --- |
| **Explain this CVE** | SCA → SBOM / Aspire SBOM, vulnerability rows | Grounded, cached explanation of a CVE *in the context of the affected package* |
| **Explain this failure** (pipeline) | `/jenkins/runs/{id}`, failed runs | Triage of *why* a pipeline run failed, from the failing job's console output |
| **Explain this failure** (deploy) | `/deployment/runs/{id}`, failed runs | Turns the typed step-failure category into a specific fix |
| **Explain this deploy** (Aspire) | `/deployment/aspire-runs/{id}`, any run with a log | Explains the aspirate log — the failure, or the warnings behind an unreachable app |
| **Assess licenses** | SCA → Visualize (`/sca/visualize/{n}`) | Turns the analyzer's license findings into a ship / don't-ship call, in priority order |
| **Explain what changed** | Aspire apps status panel, when drifted | Running-vs-deployed images: what differs, why, and whether redeploying overwrites something |
| **Weekly delivery digest** | Scheduled → Slack / email, or on demand from `/deployment/metrics` | Narrates the DORA four for the week. Opt-in; off by default |
| **Usage & cost** | AI → Usage & cost (`/ai/usage`) | Token spend, estimated cost, cache-hit rate, by model / by feature — plus build & deploy activity |

Everything else in this document is the plumbing those sit on, which is deliberately built as a
seam so later features don't each grow their own model call.
See [ai-roadmap.md](ai-roadmap.md) for what's coming and in what order.

---

## 1. Architecture

```
                     ┌─────────────────────── web-admin ───────────────────────┐
  CveExplainer ────► IAiInsightService ──► AiClient ──► Anthropic SDK ──► api.anthropic.com
  (feature)          (the only entry pt)   │  (the ONLY call site)
                                           │
                                           │ usage captured at the SDK boundary
                                           ▼
                                    IAiUsageRecorder
                                           │
                              CompositeAiUsageRecorder
                                    ┌──────┴───────┐
                                    ▼              ▼
                        MeterAiUsageRecorder   MeteringUsageRecorder
                         (OTel "Cicd.Ai")       (HTTP, fire-and-forget)
                                                       │
                     └─────────────────────────────────┼───────────────────────┘
                                                       ▼
                                         metering-api  POST /api/metering/usage/ai
                                                       │  rate → expand → persist
                                                       ▼
                                                 Metering DB (SQL)
                                                       ▲
                          /ai/usage  ── MeteringApiClient ──┘  GET summary | meters
```

Four properties this shape buys:

- **One call site.** `AiClient` is the only place the Anthropic SDK is touched. Every feature goes
  through `IAiInsightService`, so metering, model selection, and error handling can't be bypassed.
- **Usage is captured, never inferred.** Token counts come off the response's `usage` block —
  including the cache read/creation breakdown — rather than being estimated from string lengths.
- **Recording can't break the feature.** `CompositeAiUsageRecorder` isolates each sink in a
  `try/catch`, and the HTTP ingest is fire-and-forget. A metering outage costs you ledger rows, not
  the user's answer.
- **Soft-fail everywhere.** No API key means `IsConfigured == false`; the UI hides the action instead
  of throwing. This mirrors the Nexus client's behaviour — missing credentials never fail startup.

### Key files

| Path | Role |
| --- | --- |
| `src/web-admin/.../Services/Ai/IAiInsightService.cs` | The interface every feature calls |
| `src/web-admin/.../Services/Ai/AiClient.cs` | The single Anthropic SDK call site |
| `src/web-admin/.../Services/Ai/AiOptions.cs` | Config: key, model tiers, max output tokens |
| `src/web-admin/.../Services/Ai/AiModels.cs` | `AiInsightRequest` / `AiInsight` / `AiUsage` |
| `src/web-admin/.../Services/Ai/MeterAiUsageRecorder.cs` | OTel meter sink |
| `src/web-admin/.../Services/Metering/MeteringUsageRecorder.cs` | HTTP ingest sink |
| `src/web-admin/.../Services/Ai/AiExplanationRunner.cs` | The cache→call→cache half every feature shares |
| `src/web-admin/.../Services/Sca/CveExplainer.cs` | CVE explain |
| `src/web-admin/.../Services/Ci/PipelineFailureExplainer.cs` | Pipeline failure triage |
| `src/web-admin/.../Services/Deployment/DeployRunExplainer.cs` | Service-deploy failure triage |
| `src/web-admin/.../Services/Deployment/AspireRunExplainer.cs` | Aspire deploy-log explanation |
| `src/web-admin/.../Services/Sca/LicenseExplainer.cs` | License ship/don't-ship assessment |
| `src/web-admin/.../Services/Deployment/DriftExplainer.cs` | Running-vs-deployed drift explanation |
| `src/deployment/.../Features/Metrics/WeeklyDoraDigest.cs` | The scheduled delivery digest |
| `src/web-admin/.../Components/Shared/AiExplanationDialog.razor` | Generic dialog every AI panel reuses |
| `src/metering/` | The metering service (ledger, rating, endpoints) |

---

## 2. The explanation features

All of them share one shape, factored into `AiExplanationRunner`: check the distributed cache, else run
one grounded request and cache the answer. Each feature owns only what is genuinely its own — the
prompt, the model tier, the cache key, and the attribution dimensions. Empty answers are never
cached, so a transient blank doesn't pin itself for the whole TTL.

They all render through the same `AiExplanationDialog`, and all hide themselves when no key is set.

### Explain this CVE

On any vulnerability row in the SBOM panels (`SbomAnalysis.razor`, shared by `/sca/sbom` and
`/sca/aspire-sbom`) a ✨ button opens a dialog with a plain-language explanation of the CVE **as it
applies to the package that actually pulled it in**.

**It is grounded, not free-form.** `CveExplainer` builds the prompt from parsed SBOM fields only —
CVE id, scanner-reported severity, advisory source, the verbatim advisory description, the resolved
affected components (`Name @ version`), and reference URLs. The system prompt instructs the model to
ground every statement in that data, and the user prompt ends with an explicit note that CVSS score,
vector, and fixed-version ranges are *not* present and must not be fabricated. The dialog footer
labels the answer AI-generated and advisory-only.

**It is cached in Redis for 7 days**, keyed by `cve-explain:v1:{cveId}:{sorted affected components}`.
Those inputs are stable, so re-opening the same CVE — by anyone, on any build with the same affected
version — costs nothing and returns instantly. Cache hits are marked `(cached)` in the dialog and
are *not* recorded to the ledger, because no model call happened.

Runs on the **Interactive** tier and reports as feature key `explain_cve` on the usage page.

---

### Explain this pipeline failure

On a **failed** pipeline run (`/jenkins/runs/{id}`) an *Explain this failure* button appears beside
the failure banner — above the console pane, because the console is both the richest failure signal
the platform holds and the longest thing on the page.

**Substrate.** `PipelineRunConsoleLog` persists the full console text of every job in the chain,
already exposed as `JenkinsApiClient.GetPipelineRunConsoleAsync(id)` — one segment per job. The
prompt combines that with `PipelineRunDto.FailureReason` and the step record.

**Picking the failing job.** `RecordStepSucceeded` only ever appends *succeeded* steps to a run, so
the failing job is the last console segment whose job name isn't among them (falling back to the
last segment, which covers a run that failed before any step settled). That heuristic lives in
`PipelineFailureExplainRequest.FromRun` rather than the page, so it's testable and reusable.

**Console trimming.** A CI console can be megabytes and the failure is at the end, so only the
**tail** is sent — 12k chars, under the 16k tail-trim `AspireApplicationRun.Log` already uses. The
prompt states that it *is* a tail, so the model doesn't read the first line as the run's start.

**Tier.** Runs on **Synthesis** — reasoning backwards from a long, noisy build log to a cause is
what that tier was defined for, and this is its first caller. Reports as `explain_pipeline_failure`.

**Caching.** Redis, keyed `pipeline-explain:v1:{runId}`, 7 days. A settled run is immutable, so
re-opening is free and marked `(cached)`.

**Attribution.** First feature to populate `Dimensions` — it tags usage with the run's
`repository`, which turns on per-repo showback in the ledger.

### Explain this deploy failure

On a **failed** per-service deployment run (`/deployment/runs/{id}`).

**Why this one is cheap.** A deployment run has *no log* — only the typed per-step record. The deploy
pipeline already classified the failure into a `StepFailureKind` (`ToolMissing`, `RegistryAuth`,
`RegistryError`, `CloudRunAuth`, `CloudRunNotFound`, `Timeout`, `Config`), so the hard part —
categorising — is done before the model sees anything. That makes the whole prompt a short structured
record, which is why this runs on **Interactive** while pipeline triage runs on Synthesis. Paying
Opus rates to explain an already-classified failure would be waste.

The prompt carries a **legend for only the categories present on that run**, mirroring the comments
on `StepFailureKind`, so the model uses the platform's vocabulary instead of re-deriving it from the
free-text detail. It also states the deploy target explicitly (Cloud Run service vs. Kubernetes
resource) rather than letting the model infer it from which fields happen to be populated — the
target changes the remediation entirely.

Reports as `explain_deploy_failure`; tags the `service` dimension.

### Explain this Aspire deploy

On an Aspire application run (`/deployment/aspire-runs/{id}`) with a log — **including succeeded
runs**, which is the point. A run can succeed and still leave the app unreachable; that's what the
advisory alerts on that page exist to surface. So on a failure the button reads *Explain this
failure*, and on a success *Explain this deploy*, with the prompt instructed not to manufacture a
problem but to summarise what was deployed and explain any warnings.

Grounded in the aspirate log (tail-trimmed to 12k, and the prompt says whether it was truncated),
plus the manifest source, cluster/namespace, version, and the images the run reported deploying.
**Synthesis** tier — a long noisy CLI log is the same synthesis problem as pipeline triage.

Reports as `explain_aspire_deploy`; tags `service` and `environment`.

> Both deploy explainers put **run status in the cache key**. A run parked in `AwaitingPromotion` can
> later be promoted or rolled back, and the old explanation would no longer describe it — including
> status makes the entry self-invalidating on that transition.

### Assess licenses

On `/sca/visualize/{buildNumber}` (and the Aspire per-image variant), beside the license panel —
**not** `/sca/sbom`, because that's where `LicenseAnalyzer` actually runs.

`LicenseAnalyzer` has already categorised every component and written a reason per conflict, so this
is a rollup, not an analysis: which findings block or constrain shipping, which are routine, and what
order to work in. **Interactive** tier for that reason. Reports as `explain_licenses`.

**Cached on a fingerprint of the analysis, not the build number** — root category, per-category
counts, and the sorted finding set, hashed to bound the key. Rebuilds of an unchanged dependency set
have identical license posture and should share one answer.

The prompt is told three things it would otherwise get wrong: it is not a lawyer and must say so for
anything consequential; an **undeclared** licence is missing information, not evidence of a permissive
one; and the analyzer deliberately does not model classpath exceptions, LGPL static-vs-dynamic
linking, or commercial dual-licensing, so it must not imply those were considered. With zero conflicts
it is told to say so briefly rather than manufacture concerns.

### Explain what changed (drift)

On the Aspire apps status panel, when there is image drift or an undeployed change.

The prompt is made to separate two states that need **opposite** responses: an *undeployed change*
means the platform holds something newer that hasn't rolled out, while *image drift* means the cluster
is running something the platform doesn't know about — and only the second gets silently overwritten
by the next deploy. It's asked to say explicitly whether redeploying would destroy work.

**Cached on observed cluster state, 6-hour TTL.** Drift is live: keyed by app id alone, this would
keep serving an explanation of drift you'd since corrected, which is worse than none. The key includes
the running-vs-expected image set, so it changes the moment the cluster does. `explain_drift`,
Interactive tier, tagged with `service` + `environment`.

Also told what the check *doesn't* cover — image references only, not environment variables, config
maps, replica counts set outside the platform, or anything another tool applied.

### Weekly delivery digest

The first AI feature that isn't a panel: it runs on a schedule in **deployment-api** and pushes to
Slack and email via the existing `INotificationDispatcher`. `/deployment/metrics` also has a **Send
digest now** button.

**Off by default** — `Deployment:DoraDigest:Enabled` is `false`. This sends mail on a timer; nobody
should get that from pulling the branch. The manual button works either way.

| Setting | Default | |
| --- | --- | --- |
| `Deployment:DoraDigest:Enabled` | `false` | Opt-in |
| `Deployment:DoraDigest:DayOfWeek` | `Monday` | |
| `Deployment:DoraDigest:HourUtc` | `8` | |
| `Deployment:DoraDigest:WindowDays` | `7` | What the digest reports on |

**How "weekly" works.** Wolverine's scheduling is a one-shot delay, not cron, so the recurrence is a
self-rescheduling chain: the handler sends, then schedules its own successor. Durable via the SQL
outbox, so it survives restarts — unlike a process-local timer. Two properties stop the chain
multiplying:

- a **per-ISO-week marker** (`dora-digest:sent:2026-W31`) makes a duplicate fire a no-op;
- **a run that skips does not reschedule.**

So extra chains seeded by restarts die out on their first week and the steady state is one. A
`BackgroundService` seeds the first message and then exits — it starts the chain, it doesn't own the
cadence.

**Known race.** The marker is written *after* a successful send, not claimed atomically before it
(`IDistributedCache` has no compare-and-set). Two chains firing in the same instant can both send.
Send-then-mark is deliberate: a duplicate digest is recoverable, a silently skipped one isn't.

**The narrative is optional.** With no key, or on a model error, the digest sends the figures alone —
arriving without prose beats not arriving. Prompted explicitly against the failure modes this feature
invites: it has no history, so it's told not to imply direction ("improving", "up from last week"),
to state unavailable metrics plainly rather than omit or substitute them, and to flag small samples.
Reports as `dora_digest`; cached per week so a retry doesn't pay twice.

> **Two ways a digest silently goes nowhere.** `INotificationDispatcher` is fire-and-forget and
> `OnlyFailures` drops anything that isn't a `Failure` — and a digest is `Info`. So the handler logs a
> warning, and `POST /api/deployment/metrics/digest` returns `suppressed` plus a `note`; the button
> surfaces it as a warning toast instead of claiming success. It also reports each channel's
> `IsUsable`, not `Enabled` — a channel switched on without its webhook is enabled and still can't
> deliver.

---

## 3. Model tiers

Features pick a *tier*, not a model id, so the concrete model can be re-pointed in config without
touching feature code:

| Tier | Config key | Default | Used by |
| --- | --- | --- | --- |
| `Interactive` | `Ai:InteractiveModel` | `claude-sonnet-5` | CVE explain, deploy-failure explain, licenses, drift, digest — small or pre-classified inputs |
| `Synthesis` | `Ai:SynthesisModel` | `claude-opus-5` | Pipeline triage, Aspire deploy — long noisy logs |

The split is about **input shape, not importance**: a feature goes to Synthesis when it has to reason
backwards from a long, noisy log, and stays on Interactive when the platform already did the
structuring. Both failure-triage features are equally important; only one of them is hard.

> **Re-pointing either model needs a matching row in `UsageRater`**, or its cost falls through to
> the table's Opus-tier default rate.

Calls are plain non-streaming `Messages.Create` with `MaxTokens` from `Ai:MaxOutputTokens` (4096).
Extended thinking, effort, and prompt caching are *not* configured — today's caching win comes from
the Redis layer in front of the call, not from prompt caching (see Known gaps).

---

## 4. Metering & cost

`metering-api` is a general usage ledger, not an AI-only one. AI tokens are the first meter; build
and deploy activity ride the same tables.

### The ledger

One `UsageRecord` is one immutable metered sample. **One AI call expands into up to four rows** —
`input`, `output`, `cache_read`, `cache_write` — each rated independently, because the four
directions have different unit prices. Zero-token directions are skipped.

Ingest is **idempotent**: the producer supplies an `EventId`, and `EfUsageLedger` skips any
`(EventId, Direction)` already present, so a retried POST writes nothing rather than double-counting.

Every row carries `RateVersion` alongside `CostUsd`, so a historical row records which rate table
produced its cost and can be repriced later without guesswork.

### Meters

| Meter | Fed by | Quantity | Costed? |
| --- | --- | --- | --- |
| `AiTokens` | web-admin, HTTP ingest | tokens | **yes** |
| `BuildCompute` | `PipelineCompleted` on `ci.events` | jobs in the run | no |
| `DeployRun` | `ServiceDeployed` on `deployment.events` | 1 per deploy | no |
| `NexusStorage`, `DockerStorage`, `CloudRunCompute`, `K8sResource` | — | — | placeholders, not yet fed |

Build and deploy meters are **counts, not compute-seconds** — those integration events carry no
duration — so they are recorded at zero cost. The usage page says so under the table rather than
showing a misleading `$0.00`.

The build/deploy path is a genuine bus subscription: `metering-api` runs Wolverine with a SQL
outbox/inbox in its own database and subscribes to `ci.events` and `deployment.events`. If no
messaging connection string is present it still runs HTTP-only.

### Rate table

`UsageRater` (`src/metering/Metering.Application/Rating/UsageRater.cs`), version **`2026-01`**, in
USD per 1M tokens:

| Model | Input | Output | Cache read | Cache write |
| --- | --- | --- | --- | --- |
| `claude-opus-5` | $5.00 | $25.00 | $0.50 | $6.25 |
| `claude-opus-4-8` | $5.00 | $25.00 | $0.50 | $6.25 |
| `claude-sonnet-5` | $3.00 | $15.00 | $0.30 | $3.75 |
| *anything else* | falls back to the Opus tier | | | |

The fallback is deliberate: an unpriced model over-counts rather than silently costing nothing.

Two things to know about the numbers:

- **Sonnet 5 currently has promotional pricing** of $2.00/$10.00 per MTok through **2026-08-31**. The
  table uses the standard $3.00/$15.00, so during that window the page *over*-estimates Sonnet spend.
- **Re-pointing a tier at a newer model needs a matching table row.** Without one the cost falls
  through to the Opus-tier fallback — which is right for an Opus-tier model by luck, not design, and
  wrong for anything else.

Costs on the page are labelled *estimated* for these reasons — they are not a billing source of truth.

### HTTP surface

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/metering/usage/ai` | Ingest one AI call's usage (idempotent on `EventId`) |
| `GET` | `/api/metering/usage/summary?fromUtc=&toUtc=` | Rated AI rollup + by-model / by-feature breakdowns |
| `GET` | `/api/metering/usage/meters?fromUtc=&toUtc=` | By-meter totals across every meter kind |

`web-admin` POSTs to the first and the `/ai/usage` page reads the other two through
`MeteringApiClient`. Both readers no-op when `Metering:Api:BaseUrl` is unset.

### Usage & cost page

`/ai/usage`, under **AI** in the nav. Window selector (7 days / 30 days / all time), a by-meter table
across every meter kind, then AI detail: call count, estimated cost, in/out tokens, cache-hit rate,
and by-model / by-feature tables. Cache-hit rate is `cache_read / (input + cache_read)`.

### Live telemetry

Independent of the persisted ledger, two OpenTelemetry meters export to the Aspire dashboard:

| Meter | Instruments | Tags |
| --- | --- | --- |
| `Cicd.Ai` (web-admin) | `cicd.ai.tokens` | direction, feature, model |
| `Cicd.Metering` (metering-api) | `cicd.metering.tokens`, `cicd.metering.cost.usd` | direction, model |

`Cicd.Ai` records at the call site, so it works even with metering entirely unconfigured.

---

## 5. Configuration

### The API key

Empty key ⇒ AI features are disabled and the app runs normally. Never commit it.

| How you run | Where the key goes |
| --- | --- |
| Aspire host | `dotnet user-secrets set Parameters:AiApiKey <key> --project src/Aspire/Cicd.Aspire.Host` |
| docker-compose | `AI_API_KEY=<key>` in `.env` (mapped to `Ai__ApiKey`) |
| Standalone web-admin | env var `Ai__ApiKey` (double underscore) |

The Aspire host declares it as a secret parameter defaulting to empty, so a missing key never
prompts and never blocks a headless `dotnet run`.

### All settings

| Key | Env var | Default | Notes |
| --- | --- | --- | --- |
| `Ai:ApiKey` | `Ai__ApiKey` | *(empty)* | Empty ⇒ AI disabled |
| `Ai:InteractiveModel` | `Ai__InteractiveModel` | `claude-sonnet-5` | |
| `Ai:SynthesisModel` | `Ai__SynthesisModel` | `claude-opus-5` | Needs a matching `UsageRater` row if changed |
| `Ai:MaxOutputTokens` | `Ai__MaxOutputTokens` | `4096` | |
| `Ai:BaseUrl` | `Ai__BaseUrl` | *(empty)* | **Declared but not yet honoured** — see gaps below |
| `Metering:Api:BaseUrl` | `Metering__Api__BaseUrl` | *(empty)* | Empty ⇒ no ledger, OTel meter only |
| `ConnectionStrings:redis` | `ConnectionStrings__redis` | *(empty)* | Falls back to an in-process cache |

Under the Aspire host, `Metering__Api__BaseUrl` and the Redis connection string are injected for you;
only the API key is yours to supply.

### Verifying it's on

1. Start the stack and open web-admin → **SCA → SBOM**, pick a build with vulnerabilities.
2. A ✨ button on each vulnerability row means the key resolved. No button ⇒ no key.
3. Click it; the dialog should return an explanation naming your affected package.
4. Open a **failed** pipeline run (`/jenkins/runs/{id}`) — an *Explain this failure* button should
   appear beside the failure banner, and its answer should name the actual failing job. A succeeded
   run shows no button.
5. Open a **failed** deploy run (`/deployment/runs/{id}`) and an Aspire run with a log
   (`/deployment/aspire-runs/{id}`) — both should offer a button; the Aspire one offers it on
   *succeeded* runs too, worded *Explain this deploy*.
6. Open **SCA → Visualize** for a build (`/sca/visualize/{n}`) — *Assess licenses* sits beside the
   license panel. It appears even with zero conflicts; the answer should then say so briefly.
7. Open **Deployment → Aspire apps** and run a live status check. *Explain what changed* appears
   **only** when that check reports image drift or an undeployed change — no drift, no button, which
   is the intended behaviour rather than a missing registration.
8. Open **AI → Usage & cost** — the features you exercised should appear under *By feature*, split
   across `claude-sonnet-5` and `claude-opus-5` under *By model* per the tier table above.
9. Re-open any of them: the footer should read `(cached)`, and the ledger should be unchanged.

---

## 6. Known gaps

Recorded here so they aren't rediscovered as bugs:

- **The cache-hit-rate tile is currently always 0%.** `/ai/usage` computes
  `cache_read / (input + cache_read)` and `UsageRater` prices both cache directions — but `AiClient`
  sets no `cache_control`, so Anthropic never returns cached-token counts. The metric, its rating
  rows, and its dashboard tile are all inert until prompt caching is enabled (planned with the
  agentic slice, which is where a large stable prefix makes it worth having). Note this is unrelated
  to the Redis cache, which prevents calls outright rather than producing cached tokens.
- **`Ai:BaseUrl` is not wired.** `AiOptions` declares it for gateway/proxy use, but `AiClient`
  constructs `new AnthropicClient { ApiKey = … }` without it. Setting it today has no effect.
- **`Dimensions` are only partly populated.** The failure-triage feature tags `repository`;
  `CveExplainer` still passes none, and `Service` / `Environment` are unused by every caller.
- **The summary aggregates in memory.** `EfUsageLedger` pulls projected rows and rolls them up
  client-side, which is documented as a deliberate small-volume choice. It will need pushing
  server-side before the ledger gets large.

---

## 7. Extending it

To add an explanation feature, don't reach for the SDK or for `IDistributedCache` — inject
`AiExplanationRunner` and write only the parts that are yours:

1. A **grounded** prompt built from structured data you can cite. Say explicitly what is *not* in the
   data, so the model reports a gap instead of filling it.
2. A tier — `Synthesis` if it must reason backwards from a long noisy log, `Interactive` otherwise.
3. A stable `feature` key. It's the attribution key on the usage page, so don't rename it casually.
4. A cache key covering **everything the answer depends on**, including any status that can change
   later (see the deploy explainers).
5. `dimensions` (`repository` / `service` / `environment`) when the work is attributable.
6. An `IsConfigured` check at the call site so the affordance hides rather than throws.

Then render it with `AiExplanationDialog` — pass a `Loader` and a `LoadingMessage`; no new dialog.
Caching, metering, telemetry, and cost attribution all come for free. That is the point of the seam.

For anything that isn't an explanation panel, drop one level and use `IAiInsightService` directly.

---

*See also: [features.md](features.md) for the full feature catalog, [architecture.md](architecture.md)
for how the services fit together, and [sbom-setup.md](sbom-setup.md) for the SBOM pipeline that feeds
CVE explain.*
