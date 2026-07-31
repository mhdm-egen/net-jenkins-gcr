# Adoption plan

How we move from the monolith/Packer/Terraform-Enterprise pipeline onto this platform, given that
the porting work has started but the team has not yet built or operated a modernised .NET
application, nor used a CI/CD system of this kind.

Companion decks: [deck-leadership.md](deck-leadership.md) · [deck-engineers.md](deck-engineers.md) ·
[deck-platform-ops.md](deck-platform-ops.md). Long-form financial case:
[../platform-pitch.md](../platform-pitch.md).

---

## The constraint that shapes this plan

The technology is not the risk. The platform already builds, scans, publishes and deploys, and the
numbers are good. **The risk is asking people to operate an unfamiliar runtime, on an unfamiliar
pipeline, on a codebase mid-port — and to do it in production.**

Any plan that sequences by *technical* convenience will hit that wall in month two, when something
breaks in a Kubernetes namespace at 4pm and nobody can read the symptoms. So this plan sequences by
**how much new skill each step demands**, and deliberately front-loads the steps that deliver value
while demanding almost none.

Two rules follow from that, and every phase below obeys them:

> **1. Value before skill.** Each phase must pay for itself using skills the team already has.
> **2. Guardrails before autonomy.** A safety net is switched on *before* the step that needs it —
> never after the first incident proves it was needed.

---

## Phase 0 — Turn on the safety net (before anyone adopts anything)

Nobody's workflow changes. We configure the platform so that later mistakes are cheap.

- Mark production and staging **protected**, so deploys park for human approval.
- Confirm **blue-green with the health gate** is the default for whole-app deploys, so an unhealthy
  version is deleted instead of promoted.
- Enable **preview environments** with a TTL, so forgotten environments clean themselves up.
- Wire **Slack notifications** for build, pipeline and deploy outcomes, so the platform is visible
  before it is depended on.

**Exit:** a deliberately broken deploy is attempted in a scratch environment and the platform rolls
it back on its own, in front of the team. That demonstration is the point — it is what makes the
later phases feel survivable.

---

## Phase 1 — CI only, on the repo we already have (weeks 1–4)

**Point the platform's CI at the existing monolith repository. Change nothing about how it deploys.**

This is the highest-value, lowest-risk step available, and it is the one most teams skip. The
monolith keeps going out via Packer and Terraform Enterprise exactly as it does today. What the team
gains, with **no new skills required**:

- Compile feedback in minutes instead of half a day.
- A CycloneDX **SBOM and CVE scan on every build** — audit evidence that is currently assembled by
  hand, if at all.
- Build history tied to commit, author and message; notifications on every outcome.

**New skills required: none.** Developers keep working exactly as they do now; they just find out
sooner. That is what makes this phase the foundation — it buys goodwill and time.

> ⚠️ **Set expectations on build time here.** The 44-second figure in the decks is a small sample
> app. The monolith will be slower. The honest promise for Phase 1 is *"materially faster, and you
> find out about CVEs"* — not a specific number. Measure it in week one and republish the real
> figure.

**Exit:** every merge to the monolith's main branch produces a build, an SBOM and a scan, and the
team trusts the results enough to act on them.

---

## Phase 2 — Containerise the monolith, deploy to dev/test only (weeks 4–12)

There is no modular service to pilot with — the 400 projects produce **one deployable artifact**. So
the pilot is the monolith itself, containerised and deployed to dev/test through the platform, while
qa/cert/prod continue on Packer and TFE untouched.

This is where the largest single gain lands, and it needs **no decomposition**: replacing the VM bake
with a container build and promoting one pinned artifact takes commit-to-production from roughly
**9–13 hours to 30–45 minutes**. Everything after this phase is refinement of that.

- One platform-literate engineer is **paired with the team for the whole phase**. Not a hand-off, not
  office hours — paired.
- The team deploys to dev/test themselves, repeatedly. Breaking it is expected and desirable.

**New skills introduced — deliberately just three:** what a container image is; how to read a
deployment's health instead of a server's; where configuration and secrets come from now.

**The hard part is not the pipeline.** Containerising a .NET Framework monolith raises real questions:
Windows containers versus waiting for the port, local disk writes, session state, IIS assumptions,
machine-level config. Expect this phase to be dominated by those, not by the platform.

**Exit:** the team deploys the monolith to dev/test unaided and can diagnose a failed deploy without
escalating. **If they can't, stay in this phase** — do not advance on a date.

---

## Phase 2b — Wire the existing tests into the pipeline (runs in parallel)

Tests exist, but they sit **outside the main solution** and so are not part of the build. Bringing
them into the pipeline turns them from a separate activity into a gate.

- Make the suites reachable from the build, and fail the build when they fail — the same way the CVE
  gate already works.
- Start with whichever suite gives the broadest coverage per minute; the fast ones belong on every
  build, slower integration suites can gate promotion instead.
- Report duration from the first run, then add it to the timing model. Until then, every figure in
  [scaling-model.md](scaling-model.md) deliberately excludes test time on both sides.

This is an **opportunity, not a blocker** — the phases either side do not wait on it. It matters most
before Phase 5, where tests around the seams being cut are what make decomposition survivable.

**Exit:** at least one suite runs on every build and can fail it.

---

## Phase 3 — Promote through qa and cert (weeks 10–18, overlaps Phase 2)

Extend the platform up the chain: dev/test → qa → cert, still leaving production on the old path.

This is the phase that proves the central claim — **promotion rebuilds nothing.** The same pinned
artifact that was tested in dev/test is the one that reaches cert, in about two minutes per
environment rather than a fresh 2–3 hour package-and-deploy each time. Measure it here and put the
real number in the deck.

Per-PR preview environments come on in this phase too, if the monolith can stand up in one. A single
large artifact may make previews expensive; if so, defer them to Phase 5 when components are smaller.
Don't force it.

**Exit:** qa and cert are fed by promotion, and nobody re-packages to move between environments.

---

## Phase 4 — Production, behind an approval gate (weeks 16–24)

The first production deploy through the platform. Protected-environment approval stays on, on all
four environments.

- Deploy during business hours, with the pairing engineer present, and the rollback path rehearsed
  **before** it is needed.
- Run it a full month before starting Phase 5. The point is to meet a real production surprise while
  the support structure still exists.
- **Raise release cadence deliberately, not automatically.** The platform makes deploy-on-every-merge
  possible; whether to enable it is a separate decision, best taken once the Phase 2b suites are
  gating builds.

**Exit:** one month in production, including at least one rollback or incident handled by the team.

---

## Phase 5 — Modularity: the second increment (month 6 onward)

Phases 1–4 deliver roughly a 15–20× reduction with **no code restructuring**. Phase 5 attacks the
only large term left — the build — by turning one deployable artifact into ~10–20 independently
buildable and deployable ones. That takes a typical change from rebuilding 400 projects to rebuilding
one component and its dependents: **30–45 min → 12–16 min** commit to production.

- **Tests around each seam come first.** Refactoring a 400-project monolith is the highest-risk item
  on this roadmap. Existing suites cover some of it; where they don't reach the behaviour being moved,
  add characterisation tests that pin it before cutting.
- **Target the dependency graph, not the project count.** If everything references one shared
  `Common` library, a change to it rebuilds all 400 however the pipeline is configured. Depth is the
  lever.
- **Extract one component at a time**, each running Phases 2–4 in miniature.
- Preview environments become genuinely cheap here, once components are small.

**Only retire Packer/TFE for a component once it is fully across.** Running both pipelines is a real
ongoing cost — name it in status reports rather than letting it hide. **Infrastructure-as-code stays:**
TFE keeps clusters, networks, databases and IAM. What leaves it is application rollout.

---

## The enablement track (runs alongside every phase)

The phases above create the *demand* for skill; this creates the *supply*. Without it, the plan is
just a schedule.

| Capability | Who needs it | When | How |
| --- | --- | --- | --- |
| Containers: images, digests, statelessness | every developer | before Phase 2 | half-day workshop + the sample app in this repo |
| Reading a deployment: pods, health, restarts, logs | every developer | Phase 2 | pairing, on their own service |
| Config & secrets from the environment | every developer | Phase 2 | pairing + a written pattern for our stack |
| Modern .NET idioms (DI, async, hosting, options) | developers on ported code | continuous | code review by an experienced engineer, plus the porting workstream |
| Kubernetes operations | platform/ops | before Phase 4 | training + the runbooks in `docs/deployment/` |
| Registry, bus, outbox/inbox behaviour | platform/ops | before Phase 4 | walkthrough + `docs/architecture.md` |
| Incident response on the new stack | on-call | before Phase 4 | a rehearsed game-day, not a document |

**Two honest notes on enablement:**

- **The AI features genuinely lower the floor here** — "explain this failure", "explain this CVE",
  and asking the platform questions in plain English shorten the distance between *unfamiliar
  symptom* and *plausible cause*. That is a real advantage for a team in this position. They do not
  substitute for understanding, and a team that leans on them exclusively will be unable to handle
  the case the model gets wrong.
- **Buy some expertise for the first six months.** A team learning modern .NET, containers,
  Kubernetes and a new pipeline simultaneously, with no experienced practitioner alongside, is the
  single most likely way this fails. One experienced hire or contractor, embedded rather than
  advisory, is cheaper than the incident that otherwise teaches the lesson.

---

## Risks, and what we do about them

| Risk | Why it's real here | Mitigation |
| --- | --- | --- |
| **Team is overwhelmed and reverts** | Four new things at once: .NET, containers, K8s, pipeline | Phases 1–3 demand almost no new skill; pairing, not hand-off; no phase advances on a date |
| **Production incident nobody can diagnose** | No prior experience operating this runtime | Approval gates, auto-rollback, rehearsed rollback, game-day before Phase 4, pairing present at first prod deploy |
| **Two pipelines forever** | Half-migrations are the norm, not the exception | Per-component exit criteria; report the cost of running both every month |
| **The port stalls and blocks everything** | Phases 2+ depend on ported components | Phase 1 delivers value to the *unported* monolith, so CI value is not hostage to the port |
| **Key-person dependency on the platform** | One person currently holds most of this context | Runbooks first, pairing second, documented decisions in `docs/` |
| **Platform defects found late** | No automated test suite in the platform repo | Treat the first production service as a pilot; add tests to the platform before broad rollout |

---

## What this plan deliberately does *not* do

- **No big-bang cutover.** The old pipeline runs until the last component leaves it.
- **No production deploy before Phase 4.** Skill is built where mistakes are cheap.
- **No date-driven phase advancement.** Every exit is a demonstrated capability. If a team can't
  deploy unaided, the answer is another week of pairing, not a status-report green tick.
- **No claim that this replaces Terraform Enterprise.** It replaces application rollout only.

---

## What to decide now

1. **Which repository gets Phase 1** — recommendation: the monolith itself, because it needs no
   porting and proves value immediately.
2. **Which team owns the Phase 2 containerisation**, and who pairs with them.
3. **Who is the embedded expert** for the first six months, and whether we hire, contract or
   develop that person internally.
4. **Whether we fund the platform's own test suite** before it carries production traffic.

The first two can be decided this week. The platform is ready for Phase 1 today.
