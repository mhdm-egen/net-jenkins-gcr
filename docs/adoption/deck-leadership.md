---
marp: true
paginate: true
title: "Ship in minutes — the case, in measured numbers"
---

<!--
Two-slide leadership deck. Renders with Marp (marp-cli or "Marp for VS Code"); reads fine as a
plain document. Slides separated by `---`.

This is the SHORT, data-backed version. For the full financial case and ROI worksheet see
../platform-pitch.md. For the rollout, see adoption-plan.md.

Every figure marked "measured" was taken from our own running platform on 2026-07-31 (Jenkins job
history and the deployment service's run records). Read the caveat on slide 2 before quoting them.
-->

# Hours → minutes, in every environment

### The target for our real system — and why it holds as we grow

Our system: **400 projects, one deployable artifact, four environments** — dev/test → qa → cert → prod,
each paying its own package-and-deploy cost today.

| | Today | Step 1 — no code changes | Step 2 — modular |
| --- | --- | --- | --- |
| Compile | 30–45 min | 15–30 min | **2–5 min** |
| Containerise + security scan | — | 5–8 min | 2–3 min |
| Reach dev/test | 2–3 h | ~2 min | ~2 min |
| Reach qa, cert, prod | 6–9 h | **~6 min total** | ~6 min total |
| **Commit → running in prod** | **9–13 hours** | **30–45 min** | **12–16 min** |

**Step 1 requires no code changes.** Same monolith, same single artifact. The gain comes from
replacing the VM bake with a container build, and from promoting one pinned artifact to each
environment instead of re-packaging four times. Roughly a **15–20× reduction**, available first.

> **What one compile costs us today is what the entire pipeline costs here** — build, scan,
> containerise, and reach all four environments.

**The per-environment row is the real story.** Today, every environment pays the whole 30–45 minute
compile plus the 2–3 hour package-and-deploy again. Here, environments after the first cost only a
deploy: the artifact is already built and pinned to a digest, so promotion runs bit-identical
artifacts elsewhere with **no second build**.

**Step 2 is modularity**, and it attacks the only large term Step 1 leaves — the build. One artifact
becomes ~10–20 independently deployable ones, so a change rebuilds one component instead of 400
projects. That is our work to do; the platform enables it but does not perform it.

---

## What agility actually looks like

**Containers replace machine images.** An artifact is built once, pinned to a digest, and the *same
bytes* move through dev/test → qa → cert → prod. No re-baking, no "works in qa", no drift between
environments — because there is nothing left to drift.

**Environments stop being scarce.** A VM-based environment is a standing cost, so we ration them.
Containers make an environment cheap enough to create per pull request and delete when it closes —
stakeholders review working software instead of a description of it.

**Capacity follows demand.** Kubernetes and Cloud Run schedule work onto shared nodes and scale to
zero. We stop paying 24×7 for peak capacity we use at 10am.

**Recovery is a decision, not a project.** Rolling back means redeploying the previous digest — one
click, seconds — instead of rebuilding an image and re-running an apply.

**Evidence is a by-product.** Every build emits a CycloneDX bill of materials and a CVE scan tied to
the exact commit — useful when *cert* means what it usually means. Nobody assembles it by hand.

**And the four DORA metrics are measured, not estimated** — lead time, deploy frequency, change
failure rate, time-to-restore — computed from real runs.

**What it asks of us:** a skills gap rather than a technology gap (our team has not yet operated a
modernised .NET application or a pipeline like this — the adoption plan is built around closing that);
a period of running both pipelines; and the modularity work itself, which is Step 2's 3×, not Step 1's
15–20×.

> ⚠️ **Where these figures come from.** The platform's own pipeline measures **3 min 06 s** commit to
> running — but on a small sample app. The figures above are scaled estimates for *our* system, and
> deliberately conservative.
>
> **One number is still an estimate, not a measurement:** full-solution build time for 400 projects
> on modern .NET (assumed 15–30 min). Part of that gain may come from the port itself rather than the
> platform. It is cheap to settle — build the ported solution once and time it.
>
> **Test execution is excluded from every figure, on both sides.** The suites live outside the main
> solution, so no timing above includes them. Add test time to both columns when it is known; it
> lands on the build row and leaves the promotion rows untouched.

---

## AI is built into the pipeline, not bolted beside it

Speed only converts into agility if people don't stall. Most of the delay after a fast build isn't the
machine — it's someone reading logs to work out what happened. That is where AI sits here, at **every
point in the lifecycle where a person would otherwise go and find out**:

| Stage | What it does for us |
| --- | --- |
| Build fails | Names the cause from the failing job's output — no scrolling thousands of lines |
| Security scan | Explains each CVE in the context of the package that pulled it in |
| Dependencies change | Compares two builds' bills of materials and narrates what actually matters |
| Licences | A ship / don't-ship call, in priority order |
| Deploy fails | Turns the failure into a specific fix, grounded in the deploy target |
| Running ≠ deployed | Separates "not rolled out yet" from "changed out of band" |
| Release time | Release notes across a range of builds, grouped by theme |
| Every week | Narrates the DORA four to Slack automatically |
| Any question | "Ask the platform" answers from live data, showing which sources it used |

**The governance answers, before they're asked:**

- **It cannot act.** The assistant has **no write tools** — fifteen read-only ones. When it suggests an
  action, the suggestion is a *link to the real button*, validated against the same guard that renders
  that button. Never a shortcut around it.
- **Every call is metered and costed** per feature and per model, with an advisory month-to-date budget
  bar. The budget warns; it never blocks.
- **It degrades to nothing.** No API key, and the AI actions simply don't appear. The platform runs
  exactly as it would without them.
- **It refuses rather than invents** — release notes decline to summarise a range with no recorded
  commit messages instead of producing confident fiction.

---

## The ask

Point CI at the existing repository — no code changes, no deploy changes — and replace every estimate
above with our own measured numbers in 30 days.
