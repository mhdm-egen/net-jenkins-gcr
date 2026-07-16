---
marp: true
paginate: true
title: "CI/CD Platform — Proposal for DevOps Leadership"
---

<!--
Editable pitch deck. Renders as slides with Marp (marp-cli or the VS Code "Marp for VS Code"
extension); also reads fine as a plain document. Slides are separated by `---`.
Replace the [bracketed] figures in the financial section with your real numbers.
-->

# Ship in minutes, not weeks

### A CI/CD platform to replace the monolith-and-VM pipeline

Commit-pinned container builds · automated deploys · per-PR environments —
adopted **without a big-bang rewrite**.

*Proposal for DevOps leadership*

---

## What today costs us

| # | Current characteristic | What it costs |
| --- | --- | --- |
| 1 | Monolithic build, 30+ min | Slow feedback; CI is a bottleneck for every change |
| 2 | Packaging into VMs, hours | Heavy, mutable artifacts; hard to trace to a commit |
| 3 | Running on VMs | Idle capacity, manual scaling/recovery, config drift |
| 4 | Weeks to build & deploy | Big-batch releases, high blast radius, slow time-to-value |

Every one of these taxes the metrics we're measured on: **lead time, throughput, and risk.**

---

## The transformation, at a glance

| Step | Today | This platform |
| --- | --- | --- |
| **Build** | Monolith · 30+ min | Per-service, commit-pinned — build only what changed |
| **Package** | VM image · hours | Copy-not-compile container · **minutes**, digest-pinned & immutable |
| **Run** | Virtual machines | Kubernetes / Cloud Run — elastic, self-healing, declarative |
| **Release** | Weeks · big batch | Automated handoff · **same-day**; promote across environments |
| **Recover** | Manual rollback | **One-click** rollback to the exact prior digest |
| **Test a PR** | Shared/staging queue | **Preview per PR** — isolated namespace, auto-created & torn down |
| **Govern** | Tickets & tribal knowledge | Approval gates, SBOM + CVE gate, deploy history |

---

## The four numbers a manager reports on (DORA)

- **Lead time for changes:** weeks → same-day — automated build → scan → publish → deploy, no manual handoffs.
- **Deployment frequency:** batched → on every merge — auto-deploy on publish + a preview per PR.
- **Change failure rate:** lower by design — digest-pinned artifacts, approval gates, CVE gating, drift detection.
- **Time to restore (MTTR):** hours → minutes — one-click rollback to a known-good digest, plus live status & alerts.

---

# The financial case

*Where the time and money go — and where they come back.*

---

## Four places cost leaks out today

1. **Infrastructure** — always-on, over-provisioned VMs (plus per-VM OS licensing) run 24×7 whether used or not.
2. **Engineering time** — 30-minute builds and hours-long packaging are paid idle time, multiplied across every engineer, every day.
3. **Time-to-market** — weeks from "done" to "in production" delays every dollar of value a release was meant to create.
4. **Incidents & risk** — slow, manual rollback and big-batch releases raise both the odds and the cost of downtime.

---

## Where the money comes back

| Lever | Today | With the platform | Why it saves |
| --- | --- | --- | --- |
| Compute | Always-on VMs, over-provisioned | Containers on shared nodes / scale-to-zero Cloud Run | Higher density; pay for use, not idle |
| Licensing | OS license per VM | Shared container hosts | Fewer OS instances to license/patch |
| CI wait | 30+ min per build | Minutes; only what changed | Reclaimed engineer hours |
| Packaging | Hours of VM baking | Minutes; automated | Ops toil removed |
| Release toil | Manual, weeks | Automated handoff | Fewer people-hours per release |
| Incidents | Manual recovery | One-click rollback | Lower MTTR → less downtime cost |
| Compliance | Manual evidence | SBOMs stored per build | Audit-ready by default |

---

## Illustrative ROI worksheet

Replace the **[brackets]** with our real numbers — the platform's job is to shrink the first column.

**CI wait reclaimed / year**
`(30 − [new_build_min]) min × [builds_per_day] × [working_days] × [engineers] × [loaded_$_per_min]`

**Packaging toil reclaimed / year**
`([hours_per_package] − [minutes_new]) × [packages_per_week] × 52 × [loaded_$_per_hour]`

**Infrastructure**
`[VM_count] × [monthly_$_per_VM] × 12  −  [container_platform_monthly] × 12`

**Downtime avoided**
`([incidents_per_year] × [MTTR_hours_today] − [incidents_new] × [MTTR_hours_new]) × [$_per_hour_downtime]`

> These are **directional templates, not claims.** The consistent pattern: hours → minutes, weeks → same-day,
> always-on → pay-for-use. Even conservative inputs make the reclaimed engineer time and idle compute the
> largest line items.

---

## The compounding win: time-to-market

Faster lead time isn't just cost — it's **value delivered sooner**.

- A feature that shipped in *weeks* now ships *same-day*: every release's value starts accruing earlier.
- Smaller, more frequent releases mean faster learning and less rework.
- Preview environments let stakeholders see and sign off on work *before* it merges.

This is the line that turns a cost conversation into a growth conversation.

---

# The technical case

*Why the architecture is fast, safe, and hard to break.*

---

## Architecture at a glance

- **Repo-agnostic CI** — Jenkins builds **any** Git repo; the platform isn't coupled to one codebase.
- **Event-driven & decoupled** — CI publishes facts on a bus; the deployment service reacts. Neither calls the other.
- **Reliable by construction** — Wolverine + RabbitMQ with a **SQL outbox/inbox**, so events aren't lost or double-applied.
- **Clean Architecture services** (.NET 10) — Domain / Application / Infrastructure / Api, orchestrated locally by **.NET Aspire**.

`Commit → Build → Scan → Publish (Nexus) → Deploy (Kubernetes / Cloud Run)`

---

## Build & artifacts

- **Commit-pinned** — scan and publish clone the exact commit that was built; artifacts reflect exactly what ran.
- **Copy-not-compile images** — apps are published, then copied into a slim, non-root runtime image with a healthcheck. No SDK in the shipped image.
- **Immutable & digest-pinned** — images live in Nexus and deploy by `@sha256` digest, so what you test is exactly what ships.
- **Build only what changed** — per-service jobs replace the all-or-nothing monolith compile.

---

## Deploy & operations

- **Whole-app Aspire deploys** to Kubernetes (Aspir8), plus per-service Cloud Run — namespace-pinned and digest-pinned.
- **Rollback** — redeploy a previous succeeded run's exact images in seconds.
- **Promotion** — move the same pinned manifest between environments (staging → prod) with no rebuild.
- **Approval gates** — deploys to protected environments park until a human approves; reject applies nothing.
- **Live-status & drift** — real cluster health, plus detection when a running image differs from what we deployed.
- **Preview environments** — a per-PR namespace, auto-created on the branch and torn down on close.

---

## Reliability & security

- **Supply-chain security** — CycloneDX SBOMs stored in Nexus; a dependency **and** image CVE gate on every build.
- **Provenance** — every artifact traces to a commit and a build; deploy history is recorded per run.
- **Idempotent, durable events** — the SQL outbox/inbox makes the CI→deploy handoff resilient to retries and restarts.
- **Governance** — protected environments + approval gates give segregation of duties without slowing the common path.

---

## Adoption without a rewrite

1. **Point it at the existing repo** — repo-agnostic CI builds the monolith as-is: faster feedback, immutable artifacts, zero code changes.
2. **Carve off the first service** — extract one component into a container; it builds/scans/publishes independently.
3. **Turn on auto-deploy + previews** — that service deploys on publish and gets a preview per PR.
4. **Expand and add gates** — repeat service by service; add approval gates and promotion as more moves over.

**Strangler-fig, not big-bang.** Risk stays low; value starts on day one.

---

## The ask

- Stand up the platform against **one repository** and measure: build time, release lead time, and infra spend.
- Pick **one service** to carve off in the first month.
- Review the DORA metrics after 30 days and decide the rollout pace from real data.

**From weeks → to same-day. Starting with the repo we have.**
