# AI roadmap

Where the AI layer is going, and why in this order. Companion to [ai.md](ai.md), which documents
what exists today.

**Status: slices 1–2 of 9 done.**

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
| 3 | Weekly DORA digest — scheduled, pushed over the existing Slack/SMTP senders | Next |
| 4 | License risk narrative + drift explainer + release notes | Planned |
| 5 | SBOM diff — "what changed in my dependencies between two builds" | Planned |
| 6 | **"Ask the platform"** — agentic, read-only tool use across every surface | Planned |
| 7 | Suggest-and-apply — the agent proposes an action, a human applies it | Planned |
| 8 | Metering completion — gauge collectors, storage/cloud meters, GCP billing reconciliation, budgets | Planned |
| 9 | The blocked features, once their prerequisites land | Blocked |

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
- **One call site.** Everything goes through `IAiInsightService`, so metering and attribution
  can't be bypassed.

---

## Slice 9 — blocked, and on what

Each of these is a good feature whose data does not exist yet. The prerequisite is the work.

| Feature | Blocker |
| --- | --- |
| Test-failure analysis | No test results anywhere — `dotnet test` is commented out at `jenkins/build/Jenkinsfile:96`, and no trx/junit artifact is archived |
| Pod crashloop diagnosis | Kubernetes Events are never read — there is no `ListNamespacedEvent` call in the codebase; pod-level "why" is limited to phase, restarts, and logs |
| Bus-driven failure digest | No `PipelineFailed` / `BuildFailed` integration event — the bus only ever learns about success and cancellation |
| Lead-time / MTTR insight | `DoraMetrics` computes neither. Commit timestamps exist (`SourceRevision.CommittedAtUtc`) but are never joined to deploys |
| Build-failure triage from `Build` | `Build.MarkFailed` records no reason. Slice 1 uses `PipelineRun` instead, which does have `FailureReason` |

---

## Reconstructed original intent

For the record, since it isn't written down anywhere else.

| Phase | Intent (from the source) | Outcome |
| --- | --- | --- |
| Phase 0 | AI foundation seam; `IAiUsageRecorder` as OTel meter + log, deliberately no messaging dependency in web-admin | ✅ Shipped |
| Phase 1 | "First Phase-1 AI feature" — grounded, Redis-cached CVE explanations | ✅ Shipped, but it stayed the only one |
| Phase 2 | "the Phase-2 build/deploy/**storage** meters (fed from ci.events / deployment.events **and scheduled gauge collectors**)" — `Meters.cs:5-6` | ⚠️ Build/deploy counts only; storage meters and gauge collectors are slice 8 |

**Planned with scaffolding, never built** — these had enum values, config, or infrastructure
provisioned for them:

- **DORA digest** — named in `AiModels.cs:6` and `AiOptions.cs:20`; Redis was reserved for it *by
  name* in both `AppHost.cs:81` and `docker-compose.yml:144` ("cached CVE/DORA insights"). Slice 3.
- **Scheduled gauge collectors** — `MeterType.Gauge` and four storage/cloud `MeterKind` values
  exist and are unfed; `Program.cs:150` reserves the Redis cache for their snapshots. Slice 8.
- **GCP billing-export reconciliation** — `UsageRater.cs` calls it "a later slice". Slice 8.
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
