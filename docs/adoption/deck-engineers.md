---
marp: true
paginate: true
title: "What changes for you day to day"
---

<!--
Two-slide deck for the developers who will actually use this. Renders with Marp; reads fine as a
plain document. Deliberately avoids business framing — see deck-leadership.md for that.
Figures marked "measured" come from our own platform's job history on 2026-07-31.
-->

# You stop waiting

### What changes on an ordinary Tuesday

| What you do | Today | Here |
| --- | --- | --- |
| Push a change, want to know if it compiles | wait **30–45 min**, often longer in a queue | **~44 s** *(measured)* |
| Know if you introduced a CVE | find out later, or never | **every build**, automatically |
| Show someone your work | merge it, or fight for the shared environment | **its own URL per PR**, gone when you close it |
| Deploy it | raise a ticket, wait **days** | click **Deploy** |
| It broke in production | page someone, dig through logs | **one-click rollback**, then read the diagnosis |
| Find out *why* it broke | scroll thousands of log lines | ask the platform in plain English |

Build → scan → publish → containerise is **3 minutes** end to end. You will usually still be in the
same file when it finishes.

---

## AI does the log-reading

A fast build only helps if you aren't then stuck working out what it means. The platform puts an
explanation next to the thing that failed, at every stage:

| When | What you get |
| --- | --- |
| Build fails | The cause, from the failing job's console output — it picks the failing job and reads the log tail so you don't have to |
| Deploy fails | A specific fix, grounded in the deploy target and the typed failure — not generic advice |
| An Aspire deploy looks unhealthy | The warnings behind it, on a succeeded run as well as a failed one |
| A CVE appears on your SBOM | What it means *for the package that pulled it in* |
| Dependencies changed | Added / removed / upgraded / downgraded, licence changes, CVE delta — narrated in priority order |
| Running ≠ what you deployed | Whether it just hasn't rolled out, or changed out of band — and whether redeploying overwrites someone's work |
| Writing up a release | Release notes across a range of builds, grouped by theme |
| Any question | **Ask the platform** — answers from live data, and shows which tools it used to get there |

**It cannot touch your system.** Fifteen read-only tools; no write tool exists. When it suggests an
action you get a *link to the real button*, validated against the same guard that renders it — so you
are always the one who clicks.

---

## The parts that matter when you're new to this

The platform is built so that **not knowing yet is survivable**:

- **A bad build cannot reach users.** New versions go to a parallel environment and must prove
  healthy before they take traffic. If they don't, they're deleted automatically and the old
  version never stopped serving.
- **Rollback is one click**, to the exact image that was running — not a rebuild, not a guess.
- **Your PR gets a real environment**, isolated, deleted automatically. Break it freely.
- **Production still needs a human.** Protected environments hold the deploy until someone approves;
  rejecting applies nothing.

**What you will need to learn** — we're not pretending otherwise:

1. Containers: what an image is, why we pin by digest, why the app doesn't write to local disk.
2. Reading a deployment instead of a server: pods, restarts, health, logs.
3. That configuration and secrets come from the environment, not a `web.config` on a box.

You will not be asked to learn these alone or in production first — see the adoption plan.
