---
marp: true
paginate: true
title: "What replaces Packer and Terraform Enterprise — and what doesn't"
---

<!--
Two/three-slide deck for platform and ops engineers. Renders with Marp; reads fine as a plain
document. Assumes familiarity with the current Packer + Terraform Enterprise pipeline.
Figures marked "measured" come from our own Jenkins job history and deploy runs on 2026-07-31.
-->

# The pipeline, swapped part for part

| Stage | Today | Here | Measured |
| --- | --- | --- | --- |
| Compile | monolith build, 30–45 min | per-repo job, commit-pinned | **44 s** |
| Security | separate/after the fact | `cicd-scan`: CycloneDX SBOM, NuGet CVE gate, Trivy image scan | **64 s** |
| Package | **Packer bakes a VM image**, hours | container image, copy-not-compile, non-root, healthcheck | **35 s** |
| Artifact store | VM images | Sonatype Nexus — NuGet, Docker, raw SBOMs | |
| Deploy | **Terraform Enterprise apply** | deployment service → Kubernetes (Aspirate) or Cloud Run | **5 s / 64 s** |
| Rollback | rebuild or restore | redeploy the previous run's exact digest | |

`Commit → Build → Scan → Publish (Nexus) → Deploy`

**What Terraform Enterprise keeps.** This does not replace infrastructure-as-code. TFE still owns
clusters, networks, databases, IAM — the things that change monthly. What moves off TFE is
**application rollout**, which changes hourly. Those two cadences should never have shared a tool.

---

## The operational properties you'll care about

- **Digest-pinned, immutable.** Deploys reference `@sha256:…`, so what was tested is bit-identical
  to what ships. No "the tag moved".
- **Blue-green with a health gate.** New version lands in a parallel namespace, must pass health
  within ~2 minutes, then cuts over; if it fails the namespace is deleted and the old one never
  stopped serving. No service mesh required — it's a namespace/active-slot flip, not weighted
  traffic splitting. That's the honest trade for running on vanilla Kubernetes.
- **Event-driven handoff.** CI publishes facts to RabbitMQ; the deployment service reacts. Neither
  calls the other. Wolverine + a SQL outbox/inbox make the handoff idempotent across retries and
  restarts.
- **Drift detection.** The platform compares what's running against what it deployed and flags the
  difference.
- **Preview environments.** Per-PR namespace, created on branch publish, torn down on PR close, TTL
  swept if the webhook is missed.
- **Approval gates.** Protected environments park a run until a human approves — segregation of
  duties without slowing the common path.

---

## The AI layer — how it's wired, and what constrains it

One SDK call site behind an interface, with token usage captured at the boundary and fanned out to an
OpenTelemetry meter and a usage ledger. **No API key and the AI actions simply don't render** — the
platform runs unchanged without them.

| Property | Detail |
| --- | --- |
| **Read-only by construction** | The agent has 15 read tools. **There is no write tool.** It cannot mutate state even if asked |
| **Suggestions are links, not shortcuts** | A proposed action is validated against the same status guard that renders the real button, and surfaced as a link to it |
| **Grounded, not free-associating** | Failure triage reads the failing job's log tail; deploy triage reads the typed `StepFailureKind` and the target; CVE explanation reads the affected package |
| **Cached** | Explanations are Redis-cached; the agent's tool definitions + system prompt form one stable ~3,087-token cached prefix, which is what makes the cache-hit-rate figure real |
| **Metered** | Every call is rated into a ledger by model and by feature, with idempotent ingest and a versioned rate table |
| **Budgeted, advisorily** | A month-to-date spend bar with a warning threshold. It warns; it never blocks or disables |
| **Refuses rather than invents** | Release notes decline a range with no recorded commit messages rather than producing fiction |

Practical consequence for us: the AI layer is **optional infrastructure**. It fails soft, costs are
attributable per feature, and nothing in the build or deploy path depends on it being available.

---

## What this asks of us

**New things we must be able to operate**

| Area | What we need to be competent at |
| --- | --- |
| Kubernetes | namespaces, deployments, pods, ingress, image pull secrets, reading events |
| Registries | Nexus repos, auth, digest vs tag, why the daemon and the SDK resolve hosts differently |
| The bus | RabbitMQ queues, the outbox/inbox, what a stuck message looks like |
| The platform | pipelines, environments, approval gates, preview TTLs |
| The AI layer | where the key lives, how spend is metered per feature, and the budget threshold |

**Honest caveats**

- **Windows/.NET Framework components cannot use the Linux container path** until they're ported.
  Until then they stay on Packer/TFE — we will run both pipelines for a period.
- **The platform repo has no automated test suite.** Verification to date has been manual against a
  running system. If we depend on this in production, that gap needs closing.
- **Two known issues are documented and unfixed**: deleted Aspire apps orphan their run history
  (no FK cascade), and build attribution doesn't check `GitUrl`, so two repos sharing a CI job name
  would each claim every run.
- **Local Kubernetes prerequisites are fiddly** (insecure registry trust, containerd host config,
  an ingress controller). Runbooks exist — `docs/deployment/` — and they matter.
