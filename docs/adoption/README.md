# Adoption

Material for proposing this platform as a replacement for the monolith / Packer / Terraform Enterprise
build-and-deploy pipeline, and for rolling it out.

| Doc | Audience | What it is |
| --- | --- | --- |
| [deck-leadership.md](deck-leadership.md) | Leadership, decision-makers | Two slides: the before/after in measured numbers, what it buys, what it costs |
| [deck-engineers.md](deck-engineers.md) | Developers who will use it | Two slides: what changes on an ordinary day, and what they'll need to learn |
| [deck-platform-ops.md](deck-platform-ops.md) | Platform / ops engineers | Three slides: what replaces Packer and TFE, what doesn't, and what it asks of us |
| [scaling-model.md](scaling-model.md) | Anyone quoting a number | What grows as the real system grows, what doesn't, and a worksheet to estimate it |
| [adoption-plan.md](adoption-plan.md) | Everyone | The phased rollout, sequenced by skill demand rather than technical convenience |
| [platform-case.html](platform-case.html) | All of the above | **The whole thing as one self-contained page** — all four audiences behind a tab switcher. Open it in any browser; no build step, no network access |
| [../platform-pitch.md](../platform-pitch.md) | Leadership | The earlier long-form deck: full financial case and ROI worksheet |

The three decks render as slides with [Marp](https://marp.app/) — `marp-cli`, or the "Marp for VS Code"
extension — and read fine as plain documents. Slides are separated by `---`.

**Which to use when:** present from `platform-case.html` (one page, tab per audience, works offline);
review and edit the markdown, which stays the source of truth in a PR. When you change one, change the
other — they are maintained by hand, not generated.

## Where the numbers come from

Every figure marked *measured* was taken from this platform's own records on **31 July 2026**:

| Source | Figure |
| --- | --- |
| Jenkins `cicd-build` | 43.6 s average (33.9–55.9), successful builds |
| Jenkins `cicd-scan` | 64.2 s average |
| Jenkins `cicd-publish-nexus-nuget` | 38.4 s average |
| Jenkins `cicd-publish-nexus-docker` | 34.5 s average |
| Jenkins `cicd-aspire-publish` | 65.5 s average |
| Deployment service, Aspire → Kubernetes | 5.4 s average over 7 runs |
| Deployment service, Cloud Run | 63.8 s average over 2 runs |

Derived: full CI chain **3 min 01 s**; commit → running **3 min 06 s**.

**Three caveats that must travel with the numbers:**

1. They were measured against a **sample application**, not the .NET 4.7 monolith. A real system is
   slower to build, which is why the decks quote scaled estimates rather than these figures. See
   [scaling-model.md](scaling-model.md) for which terms grow (compile) and which don't (the health
   gate is capped at 120 s; promotion rebuilds nothing).
2. **Test execution is excluded from every figure, on both sides of the comparison.** Our suites live
   outside the main solution and are not part of the build being measured. Add test time to both
   columns when it is known — it lands on the build row, not the promotion rows.
3. **Change failure rate is deliberately omitted.** The sandbox's figure reflects deploy failures
   induced on purpose during testing and would misrepresent quality if quoted.

Re-measure against a real repository during Phase 1 and republish the figures — that is the point of
Phase 1.
