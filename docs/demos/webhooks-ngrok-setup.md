# Exposing the Git Webhook with ngrok

The inbound git webhook lives on **jenkins-api** at `POST /api/jenkins/webhooks/git`, bound to
`http://localhost:7229` (pinned in [AppHost.cs](../../src/Aspire/Cicd.Aspire.Host/AppHost.cs); see
[build-pipeline-demo.md → Webhooks locally](build-pipeline-demo.md#webhooks-locally) for the routing
rules and payload shape). It is **not publicly reachable**, so to receive a callback from a real
GitHub / GitLab repository you tunnel that port with ngrok.

> Because the port is pinned to `7229`, the tunnel target is stable across stack restarts — you no
> longer have to look the port up each session.

## 1. One-time: authenticate ngrok

ngrok 3.x is already installed on this machine (`ngrok version` to confirm). Grab your token from
<https://dashboard.ngrok.com/get-started/your-authtoken> and register it once:

```bash
ngrok config add-authtoken <YOUR_TOKEN>
```

## 2. Start the tunnel

```bash
ngrok http 7229
```

ngrok prints a public HTTPS URL, e.g. `https://a1b2-73-x.ngrok-free.app`. Your webhook **callback
URL** is that base plus the path:

```
https://a1b2-73-x.ngrok-free.app/api/jenkins/webhooks/git
```

Keep the window open — the **request inspector at <http://localhost:4040>** shows every inbound
request and lets you *replay* them, which is gold for a live demo.

> Free-tier URLs change on each `ngrok http` run. Claim your one free **static domain**
> (dashboard → Domains) and run `ngrok http 7229 --url=https://your-name.ngrok-free.app` so the
> provider config never has to change.

## 3. Smoke-test the tunnel (before touching a git provider)

Prove `internet → ngrok → jenkins-api` end to end with the platform's **normalized** payload:

```bash
curl -sS -X POST "https://a1b2-73-x.ngrok-free.app/api/jenkins/webhooks/git" \
  -H 'Content-Type: application/json' \
  -d '{"repository":"aspire-sample","branch":"feature/x","action":"opened","prNumber":42,"appName":"sampleapp"}'
# → {"outcome":"build-triggered","runId":"..."}
#   other outcomes: default-branch-skipped | repo-not-found | not-an-aspire-repo | ignored-action:<a>
```

An `outcome` in the response means the path works. (`repository` / `appName` must match what you
registered under **CI → Repositories**.)

## 4. Wire a real git provider — mind the payload shape

The endpoint expects the platform's **normalized** JSON (`{repository, branch, action, prNumber,
appName}`), **not** GitHub's raw `pull_request` payload. A native GitHub/GitLab webhook pointed
straight at the ngrok URL delivers the wrong shape and comes back `branch-required` /
`action-required`. Two ways to bridge it:

### Option A — GitHub Actions reshapes it (recommended, no server to run)

A workflow on `pull_request` events curls the normalized shape to your ngrok URL. This is a genuine
provider callback and does the mapping for you:

```yaml
# .github/workflows/notify-cicd.yml
on:
  pull_request:
    types: [opened, synchronize, reopened, closed]
jobs:
  notify:
    runs-on: ubuntu-latest
    steps:
      - run: |
          curl -sS -X POST "${{ secrets.CICD_WEBHOOK_URL }}/api/jenkins/webhooks/git" \
            -H 'Content-Type: application/json' \
            -d '{
              "repository": "aspire-sample",
              "branch": "${{ github.head_ref }}",
              "action": "${{ github.event.action }}",
              "prNumber": ${{ github.event.number }},
              "appName": "sampleapp"
            }'
```

Store the ngrok base URL as the `CICD_WEBHOOK_URL` repo secret. `repository` / `appName` must match
the registered CI repository.

### Option B — native webhook + a tiny adapter

Point GitHub's webhook at ngrok, but run a small reverse-proxy that reshapes GitHub's JSON → the
normalized body → forwards to `http://localhost:7229/api/jenkins/webhooks/git`. More moving parts;
only worth it if you can't add a workflow to the repo. The endpoint has **no HMAC/secret
verification**, so for a native webhook leave the secret blank — a dev-only posture.

## 5. Things that will trip you up

- **Free URL drift** — use a reserved static domain (step 2) or you re-configure the provider each session.
- **Payload shape** — native provider payloads won't match; use Option A or B above.
- **No signature check** — the endpoint trusts the body and always returns 200; fine for local/dev, not for a public deployment.
- **Routing rules** — only a **registered, Aspire-kind** repo triggers; the **default branch is
  skipped** (that's a normal deploy, not a preview); `opened/synchronize` on a feature branch builds
  a preview; `closed/merged` tears it down. Full detail in
  [build-pipeline-demo.md → Webhooks locally](build-pipeline-demo.md#webhooks-locally).

## What "success" looks like

`opened` on a feature branch → a pipeline run starts (`CI → Pipeline runs`) and a per-PR preview
environment materializes (`Deployment → Previews`). `closed` → jenkins-api calls the deployment
teardown webhook and the preview is reaped. Watch it live in the ngrok inspector (`:4040`) and the
web-admin.
