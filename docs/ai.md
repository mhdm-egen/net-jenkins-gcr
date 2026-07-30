# AI features

The platform has an AI layer built on the official Anthropic SDK, plus a **metering service** that
turns every model call into a rated, queryable cost ledger. Both are optional: with no API key the
app runs exactly as before, AI actions simply don't appear.

Three things ship today:

| Feature | Where | What it does |
| --- | --- | --- |
| **Explain this CVE** | SCA → SBOM / Aspire SBOM, vulnerability rows | Grounded, cached explanation of a CVE *in the context of the affected package* |
| **Explain this failure** | Pipeline run detail (`/jenkins/runs/{id}`), failed runs only | Grounded, cached triage of *why* a pipeline run failed, from the failing job's console output |
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
| `src/web-admin/.../Services/Sca/CveExplainer.cs` | The CVE-explain feature (prompt + cache) |
| `src/web-admin/.../Services/Ci/PipelineFailureExplainer.cs` | The failure-triage feature (prompt + cache) |
| `src/web-admin/.../Components/Shared/AiExplanationDialog.razor` | Generic dialog every AI panel reuses |
| `src/metering/` | The metering service (ledger, rating, endpoints) |

---

## 2. Explain this CVE

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

## 3. Explain this failure

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

---

## 4. Model tiers

Features pick a *tier*, not a model id, so the concrete model can be re-pointed in config without
touching feature code:

| Tier | Config key | Default | Used by |
| --- | --- | --- | --- |
| `Interactive` | `Ai:InteractiveModel` | `claude-sonnet-5` | Latency-sensitive UI panels — CVE explain |
| `Synthesis` | `Ai:SynthesisModel` | `claude-opus-5` | Deep synthesis — pipeline failure triage |

> **Re-pointing either model needs a matching row in `UsageRater`**, or its cost falls through to
> the table's Opus-tier default rate.

Calls are plain non-streaming `Messages.Create` with `MaxTokens` from `Ai:MaxOutputTokens` (4096).
Extended thinking, effort, and prompt caching are *not* configured — today's caching win comes from
the Redis layer in front of the call, not from prompt caching (see Known gaps).

---

## 5. Metering & cost

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

## 6. Configuration

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
5. Open **AI → Usage & cost** — two calls, non-zero tokens, a cost, and both `explain_cve` and
   `explain_pipeline_failure` under *By feature* (the latter against `claude-opus-5`).
6. Re-open either one: the footer should now read `(cached)`, and the ledger should be unchanged.

---

## 7. Known gaps

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

## 8. Extending it

To add an AI feature, don't reach for the SDK — inject `IAiInsightService` and:

1. Build a **grounded** prompt from structured data you can cite. Say explicitly what is *not* in the
   data so the model doesn't fill the gap.
2. Pick a tier (`Interactive` or `Synthesis`) rather than a model id.
3. Choose a stable `Feature` key — it's the attribution key on the usage page.
4. Pass `Dimensions` (`repository` / `service` / `environment`) if the work is attributable.
5. Cache in `IDistributedCache` when the inputs are stable, keyed so the key changes when they do.
6. Check `IsConfigured` and hide the action when false — never throw at the user.

Metering, telemetry, and cost attribution then come for free; that is the point of the seam.

---

*See also: [features.md](features.md) for the full feature catalog, [architecture.md](architecture.md)
for how the services fit together, and [sbom-setup.md](sbom-setup.md) for the SBOM pipeline that feeds
CVE explain.*
