# AI roadmap

Where the AI layer is going, and why in this order. Companion to [ai.md](ai.md), which documents
what exists today.

**Status: all 9 slices done.**

> **Release notes was cut from slice 4 and delivered in slice 5 — the deferral was right.** The stated
> reason was that `BuildSummaryDto` carried no commit message or author. Building it properly showed
> the cause was narrower than it looked: `SourceRevision` had `Author` / `Message` / `CommittedAtUtc`
> all along, and the `CommitAuthor` / `CommitMessage` columns have existed **since the `InitialCi`
> migration**. Nothing was missing but the *producer* — the Jenkinsfile never recorded them, so the
> sync service hardcoded `null` and the DTO never exposed them.
>
> Doing it in slice 5 meant fixing the whole chain once (Jenkinsfile → `build-info.json` → sync →
> contract → UI) rather than bolting a narrative onto data that wasn't there. A single-build summary
> in slice 4 would have shipped the weaker half of the feature and still needed this work afterwards.

---

## Why this document exists

No AI design document ever existed in this repo. The original phase plan survives only in code
comments and commit bodies, which meant "what did we plan?" had to be reconstructed from source
before it could be extended. That reconstruction is below, so it doesn't have to happen twice.

The reconstruction turned up a real gap: **Phase 2 shipped narrower than its own code comment
describes**, and several features had infrastructure provisioned *by name* and were never built.

---

## The program

| # | Slice | Status |
| --- | --- | --- |
| 1 | **Pipeline failure triage** — "why did this run fail" on a failed run's page | ✅ Done |
| 2 | **Deploy failure explainer + Aspire deploy-log explainer** — plus `AiExplanationRunner`, the shared cache→call→cache half all features now use | ✅ Done |
| 3 | **Weekly DORA digest** — Wolverine self-rescheduling chain + manual trigger, pushed over the existing Slack/SMTP senders. Included extracting `Cicd.Ai` so a second host could use it, and moving the DORA computation server-side with lead time + MTTR added | ✅ Done |
| 4 | **License assessment + drift explainer.** Release notes was dropped — see below | ✅ Done |
| 5 | **Commit-range capability**: SBOM diff between two builds + commit provenance + release notes over the range | ✅ Done |
| 6 | **"Ask the platform"** — agentic, read-only tool use across every surface; carried prompt caching | ✅ Done |
| 7 | **Suggest-and-apply** — the agent proposes a validated action, a human applies it behind the existing gate | ✅ Done |
| 8 | **Metering completion** — gauge collectors, Nexus/Docker storage meters, budgets. Cloud-compute metering + GCP billing reconciliation are one deferred piece, not two — see below | ✅ Done |
| 9 | **The blocked features** — reassessed against current code; the failure event unblocked and consumed, the test-results prerequisite cleared. What remains blocked is recorded with what it needs | ✅ Done |

Ordering rationale: slices 1–2 attack the biggest real toil (failure triage) and establish the
reusable explain-a-run pattern; slice 3 closes the original plan and is the one that reaches
leadership; slices 4–5 are cheap once the pattern exists; slice 6 is the breadth play and the
natural home for prompt caching and the first large stable prompt prefix; 7 layers on 6; 8 is
independent; 9 waits on prerequisites.

### Standing design constraints

- **Grounded prompts only.** Every prompt is assembled from structured data the platform already
  holds, and says explicitly what is *not* in the data so the model doesn't fill the gap.
- **Read-only tools.** For any agentic slice the model gets read tools and never a write tool.
  Writes go through the existing endpoints behind the existing confirm gates.
- **Soft-fail.** No API key means the affordance is hidden, never broken.
- **One call site.** Everything goes through `IAiInsightService` or — since slice 6 — the agentic
  `IAiAgentService`. Both are implemented by the same `AiClient` instance, so there is still exactly
  one place the SDK is touched and metering and attribution can't be bypassed.

---

## The blocked features — what cleared, and what still hasn't

Each of these was a good feature whose data did not exist. Slice 9 re-checked every one against the
current code rather than trusting the original reconstruction — which was worth doing, since one
citation had already gone stale. Struck-through rows are cleared.

| Feature | Blocker |
| --- | --- |
| Test-failure analysis | **Prerequisite cleared in slice 9, feature still to build.** `dotnet test` now runs behind an opt-in `RUN_TESTS` parameter and archives a `.trx`. It is off by default deliberately: turning tests on unconditionally changes what an existing build *means* — a repo with failing or absent tests would start failing builds that pass today — and that is the pipeline owner's call. **No build has produced a `.trx` yet**, so the analysis feature has nothing to read and was not built on unverifiable ground. *(The old citation here said line 96; a slice-5b edit had already moved it to 112 — line numbers in this document are not load-bearing and should be re-checked, not trusted.)* |
| Pod crashloop diagnosis | Kubernetes Events are never read — there is no `ListNamespacedEvent` call in the codebase; pod-level "why" is limited to phase, restarts, and logs |
| ~~Bus-driven failure digest~~ | **Unblocked in slice 9.** `Ci.PipelineFailed` is now published on `ci.events` with the pipeline context, the recorded failure reason, and the steps that *did* complete. The surprise was how little was missing: the `PipelineRunFailed` **domain** event had been raised all along — it just had no translator and no integration event, so failure never left the CI service. Metering consumes it as the first subscriber |
| ~~Lead-time / MTTR insight~~ | **Unblocked in slice 3.** `ContainerPublished` now carries the commit timestamp and the run snapshots it, so lead time is genuinely commit→production. It reports unavailable until the first post-change deploy — there is no honest substitute (see `LeadTimeBasisDto`) |
| Build-failure triage from `Build` | `Build.MarkFailed` records no reason. Slice 1 uses `PipelineRun` instead, which does have `FailureReason` |
| Cloud-compute metering (`CloudRunCompute`) **and** GCP billing reconciliation | The same blocker, so the same work: `ListCloudRunServicesAsync` returns name/URL/revision/status and nothing billable — no CPU, memory, instance count or time. Needs the Cloud Monitoring API or the billing export; neither client is referenced anywhere in the repo. Deferred out of slice 8 for this reason |
| `K8sResource` metering | Counts (namespaces, workloads, pods) are readable today, but no DTO carries CPU/memory **requests**, so nothing can be costed. A pod count in a cost ledger invites being read as spend — the operational count already lives on the Kubernetes pages |

---

## Reconstructed original intent

For the record, since it isn't written down anywhere else.

| Phase | Intent (from the source) | Outcome |
| --- | --- | --- |
| Phase 0 | AI foundation seam; `IAiUsageRecorder` as OTel meter + log, deliberately no messaging dependency in web-admin | ✅ Shipped |
| Phase 1 | "First Phase-1 AI feature" — grounded, Redis-cached CVE explanations | ✅ Shipped, but it stayed the only one |
| Phase 2 | "the Phase-2 build/deploy/**storage** meters (fed from ci.events / deployment.events **and scheduled gauge collectors**)" — `Meters.cs:5-6` | ✅ Completed in slice 8 — the storage meters and the scheduled gauge collector now exist. Cloud meters remain out (no billable data source) |

**Planned with scaffolding, never built** — these had enum values, config, or infrastructure
provisioned for them:

- **DORA digest** — named in `AiModels.cs:6` and `AiOptions.cs:20`; Redis was reserved for it *by
  name* in both `AppHost.cs:81` and `docker-compose.yml:144` ("cached CVE/DORA insights"). Slice 3.
- **Scheduled gauge collectors** — `MeterType.Gauge` and four storage/cloud `MeterKind` values
  existed unfed for the whole life of the ledger. **Built in slice 8** — see `StorageGaugeCollector`.
  The Redis reservation turned out not to be needed: the collector posts each sample straight to the
  ledger, and a gauge is a level, so there is nothing worth caching between runs.
- **GCP billing-export reconciliation** — `UsageRater.cs` calls it "a later slice". Assessed in slice 8
  and deliberately still deferred: it is the same work as `CloudRunCompute` metering, and neither the
  Cloud Monitoring nor the billing-export client exists in the repo.
- **Compute-seconds metering** — commit `ef27cc3`: "compute-seconds needs enriched events later".
  Needs duration on the CI/deploy events first.

**Named only as examples, never designed** — "deploy advisor", "remediation", and "agentic" appear
solely in the `Synthesis` tier's doc-comment as illustrations of what that tier is for. Slices 2,
4, 6, and 7 are what those illustrations turn into.

**One deliberate reversal:** Phase 0 predicted usage would reach the ledger as an
`AiTokensConsumed` integration event over the Wolverine outbox. It shipped as fire-and-forget HTTP
because web-admin is a Blazor UI host, not a bus participant. Keeping `IAiUsageRecorder` a seam is
what made that substitution a one-file change.

---

*See [ai.md](ai.md) for what exists today, including its known gaps.*
