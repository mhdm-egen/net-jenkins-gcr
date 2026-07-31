# How this scales to a real application

The measured figures in the decks come from a sample app. A real system is larger, so the honest
question is not *"will it still be 3 minutes?"* — it won't — but **which parts grow, which don't, and
what the realistic target is.**

Short answer: **build time grows, deploy time barely does, and promotion between environments doesn't
grow at all.** A 5–10 minute commit-to-running target is realistic, and the gap versus today stays
enormous because today's pipeline pays its full cost *per environment*.

---

## What grows, and what doesn't

| Stage | Scales with | Growth |
| --- | --- | --- |
| Compile | changed projects, LOC | **Linear** — but divisible by build agents, and only what changed |
| Tests | test count in changed scope | **Linear** — usually the dominant term; parallelisable |
| Container build | publish output size | **Sub-linear** — layer caching means unchanged layers aren't rebuilt |
| SBOM + CVE scan | dependency count | **Near-flat** — dependencies grow far slower than code |
| Image pull | image size | **Near-flat after first pull** — layers cache on the node |
| Health gate | *nothing* | **Fixed ceiling: 120 s**, and it passes as soon as the app is healthy |
| Promotion to the next environment | *nothing* | **Zero build** — redeploys the same pinned digest |

The two rows that matter most are the last two, and both are verified in code, not assumed:

- The health gate polls every 5 s against a hard **120-second deadline**
  (`AspireApplicationRunExecutor.cs:146`). Deploy duration is bounded by readiness, not by codebase
  size.
- Promotion creates a run against a different environment using the app's **existing**
  `ManifestSource` and `Version` (`PromoteAspireDeployment.cs:58-59`). Because deploys are
  digest-pinned, promoting runs bit-identical artifacts elsewhere. There is no second build.

---

## A worksheet you can fill in

Replace the variables with our real numbers. The point is the *shape*, not the precision.

```
  s   = deployable services touched by a typical change   (often 1)
  P   = parallel build agents available
  c   = compile time per service                          (min)
  t   = test time per service                             (min)
  k   = container build + push per service                (min)
  z   = SBOM + CVE scan                                   (min, roughly fixed)
  d   = deploy time per environment                       (min, bounded ~1–3 by the health gate)
  E   = environments a release traverses (dev, staging, prod …)

  First build of a change   ≈  ceil(s / P) x (c + t + k)  +  z
  Deploy to dev             ≈  d
  Promote to each higher env≈  d                          (no rebuild, ever)

  Commit → running in dev   ≈  ceil(s / P) x (c + t + k) + z + d
  Commit → running in prod  ≈  ceil(s / P) x (c + t + k) + z + (E x d)
```

### Illustrative, at the 5–10 minute target

With `s=1, P=2, c=2.0, t=2.5, k=1.0, z=1.0, d=2.0, E=3`:

| | Today | This platform |
| --- | --- | --- |
| Commit → running in dev | ~3 hours | **~8.5 min** |
| Promote dev → staging | another full cycle | **~2 min** |
| Promote staging → prod | another full cycle | **~2 min** |
| **Commit → running in prod** | **~3 hours × 3 environments** | **~12.5 min total** |

The per-environment row is where the ratio actually lives. Today every environment pays the whole
30–45 minute compile plus the 2–3 hour package-and-deploy again. Here, environments after the first
cost only a deploy — because the artifact is already built and pinned.

---

## Our system: 400 projects, one artifact, four environments

Confirmed inputs: **400 projects producing a single deployable artifact with no modularity**, a
package-and-deploy cost paid **per environment** across dev/test → qa → cert → prod, and **test
execution excluded from all timings** (the suites live outside the main solution).

### Today's real cost

| | |
| --- | --- |
| Compile | 30–45 min |
| Package &amp; deploy × 4 environments | 8–12 hours |
| **Commit → running in production** | **≈ 9–13 hours of pipeline** |

That is machine time alone, before approvals — which take days — and before the 1–3 week batching
that follows from a cycle this expensive.

### The same work on this platform

The headline finding: **most of the win arrives before any modularity work.**

| | Today | Increment 1 — as-is | Increment 2 — modular |
| --- | --- | --- | --- |
| Build | 30–45 min | 15–30 min *(whole solution)* | **2–5 min** *(only what changed)* |
| Containerise + scan | — | 5–8 min | 2–3 min |
| Deploy to dev/test | 2–3 h | ~2 min | ~2 min |
| Promote → qa, cert, prod | 6–9 h | **~6 min total** | ~6 min total |
| **Commit → prod** | **≈ 9–13 h** | **≈ 30–45 min** | **≈ 12–16 min** |

**Increment 1 requires no code changes at all** — same monolith, same single artifact. The gain comes
entirely from replacing the VM bake with a container build and from promoting a pinned digest instead
of re-packaging per environment. That is roughly a **15–20× reduction**, available first.

> **The line worth remembering:** what one compile costs today (30–45 min) is what the entire
> pipeline costs here — build, scan, containerise, and reach *all four* environments.

**Increment 2 is modularity**, and it attacks the only term Increment 1 leaves large: the build. Going
from one artifact to ~10–20 independently deployable ones takes a typical change from rebuilding 400
projects to rebuilding one and its dependents.

The lever is the **depth of the dependency graph**, not the project count. If every project
references one shared `Common` library, a change to it rebuilds all 400 however the pipeline is
configured. Decomposition should target that graph, not the file count.

### Tests are excluded from every figure here

Tests exist, but they live **outside the main solution** and are not part of the build being measured.
Every duration on this page is therefore **build-and-deploy time only, with no test execution in it —
on both sides of the comparison**, so the ratio stays fair.

Two notes, without inflating them:

- **When test time is known, add it to both columns.** It lands on the *build* side of the model, so
  it shifts the "commit → dev/test" row and leaves the promotion rows untouched — which is the row
  most of the four-environment gain comes from.
- **Wiring the existing suites into the pipeline as a gate is a follow-on opportunity**, not a
  prerequisite. The platform already fails a build on CVE findings; test results would gate the same
  way once the suites are reachable from the build.

### The one estimate still to pin down

**Full-solution build time for 400 projects on modern .NET.** Everything above uses 15–30 minutes,
which is an estimate, not a measurement. The current 30–45 min figure is .NET Framework 4.7 tooling;
.NET 10 with parallel MSBuild is typically faster on identical code, so part of the gain may come
from the port itself rather than the platform — worth not over-claiming.

This is cheap to settle: build the ported solution once and time it. It should be the first thing
Phase 1 measures.

---

## The assumption most likely to break this

**"Build only what changed" is not automatic.** It is real at the *repository* level — per-repo,
per-service jobs — and at the *workload* level on deploy, where applying a full manifest only restarts
the workloads whose image digest actually changed. It is **not** automatic incremental build *within*
a 400-project solution.

That is exactly why the Step 1 / Step 2 split matters. Step 1's 15–20× needs no restructuring at all.
Step 2's further 3× is bought by decomposing into independently buildable units, and that is
engineering work, not configuration. It is the main lever on `s` and `c` in the worksheet above.

---

## What to measure during Phase 1

Phase 1 of the [adoption plan](adoption-plan.md) points CI at the real repository. That is the moment
to replace every variable above with a measured value:

1. `c` — actual compile time for the real codebase. **The single most valuable measurement**, and the
   one number in this document that is still an estimate.
2. `s` — how many deployable units a typical change really touches (today: one).
3. `k`, `z` — container build and scan, which should stay close to the sample figures.
4. `t` — test duration, *if and when* the suites are wired into the build. Excluded from every figure
   here by design.

Then republish the table with our own numbers. **That is the point of Phase 1** — the platform's
value proposition should rest on our measurements, not a sample app's.
