# Getting Started

How to run the platform locally and where the pieces live. For what it is and why, see the
[README](../README.md); for the full picture and diagrams, see [architecture.md](architecture.md).

## Service map

| Resource | Project | Role |
| --- | --- | --- |
| `jenkins-api` | `src/jenkins/Jenkins.*` | Orchestrates Jenkins jobs, polls builds, reconciles Nexus artifacts, raises CI events |
| `deployment-api` | `src/deployment/Deployment.*` | Services × Environments × Mappings, container inventory, deploy runs; Cloud Run + Aspire→K8s |
| `web-admin` | `src/web-admin/cicd.web.admin` | Blazor UI over both APIs (Jenkins, Nexus, SCA/SBOM, Deployment) |
| `sql` | SQL Server (container) | `JenkinsCi` + `Deployment` databases |
| `messaging` | RabbitMQ (container) | `ci.events` / `deployment.events` fanout channels |
| `jenkins` | Jenkins controller (container) | pipeline execution. Built from `jenkins/controller/Dockerfile`; plugins, credentials and the five `cicd-*` jobs are applied by JCasC on boot. Volume `jenkins-home` |
| `nexus` | Sonatype Nexus 3 (container) | artifact storage. Repositories, realms and the `:8082` docker connector are created by the `nexus-provision` one-shot. Volume `nexus-data` |

## Run it

Everything comes up from the **Aspire AppHost** — one command starts SQL Server, RabbitMQ,
`jenkins-api`, `deployment-api`, and `web-admin`:

```bash
dotnet run --project src/Aspire/Cicd.Aspire.Host
```

The console prints an **Aspire dashboard** URL (with a login token). Open it, wait for resources to go
green, then click the **web-admin** endpoint **from the dashboard** — that instance receives the
Aspire-assigned API URLs. (A web-admin started on its own falls back to a fixed port and can't find the
deployment-api.)

**Prerequisites:** Docker Desktop running (SQL Server + RabbitMQ run as containers); .NET 10 SDK. For the
Aspire→K8s deploy + preview features, enable Kubernetes in Docker Desktop (context `docker-desktop`).

#### One-time: trust the Nexus registry (required for the CI publish jobs)

Nexus serves its docker registry over **plain HTTP** on `:8082`, so the docker daemon must be told to
trust it. Without this, `cicd-publish-nexus-docker` and `cicd-aspire-publish` fail at the login/push
step. Add `host.docker.internal:8082` to `insecure-registries` in `~/.docker/daemon.json` (Docker
Desktop → Settings → Docker Engine), then **restart Docker**:

```json
{
  "insecure-registries": ["host.docker.internal:8082"]
}
```

Verify with `docker info | grep -A3 'Insecure Registries'`.

> **Why `host.docker.internal` and not `nexus`?** The registry hostname has to resolve in two
> different places. `docker login`/`push` is resolved by the **daemon**, while the .NET SDK
> (`dotnet publish -t:PublishContainer`, used by aspirate) connects **from inside the build agent**.
> A Docker Desktop daemon cannot resolve a container name on a user-defined network, so `nexus:8082`
> fails there with `lookup nexus: no such host`; conversely `localhost:8082` resolves for the daemon
> but points at the agent itself, giving `CONTAINER1013 ... Connection refused`.
> `host.docker.internal:8082` works for both, is dotted (the SDK rejects single-label hosts with
> `CONTAINER2012`), and is the same address cluster nodes pull from in
> [deployment/aspire-k8s-local-runbook.md](deployment/aspire-k8s-local-runbook.md). The
> `NEXUS_SDK_HOST` job parameter exists to split the two if your setup ever needs it.

#### One-time: Kubernetes, for the deploy half

Only needed for Aspire→K8s deploys and preview environments; CI works without a cluster.

**1. A cluster.** Enable Kubernetes in Docker Desktop (context `docker-desktop`), or use kind.

**2. Let the nodes pull from Nexus.** The registry is authenticated and plain HTTP, so the node's
container runtime has to trust it — the image-pull secret alone is not enough. On Docker Desktop's
Kubernetes:

```bash
docker exec desktop-control-plane sh -c 'mkdir -p "/etc/containerd/certs.d/host.docker.internal:8082" && cat > "/etc/containerd/certs.d/host.docker.internal:8082/hosts.toml" <<EOF
[host."http://host.docker.internal:8082"]
  capabilities = ["pull", "resolve"]
  skip_verify = true
EOF'
```

Verify with `docker exec desktop-control-plane crictl pull --creds admin:<nexus-password> host.docker.internal:8082/apiservice:latest`.
For kind, `samples/aspire-sample/kind-nexus-setup.sh` does the equivalent across all nodes. The
image-pull secret itself is provisioned for you (`Deployment:Aspirate:EnsurePullSecret`, on by default).

**3. An ingress controller**, or deployed apps have no reachable URL:

```bash
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.13.9/deploy/static/provider/cloud/deploy.yaml
kubectl wait -n ingress-nginx --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller --timeout=300s
```

> **Symptom if you skip it:** the app URL fails with `ERR_CONNECTION_REFUSED` even though the deploy
> succeeded and the pods are `Running`. The tell is that `kubectl get ingress -A` shows the Ingress
> with an **empty ADDRESS**, and `kubectl get ingressclass` returns *no resources* — the Ingress
> objects exist but nothing implements `class: nginx`, so nothing is listening on :80.
> `*.localtest.me` resolves to 127.0.0.1, which Docker Desktop maps to the controller's
> LoadBalancer. A bare `http://localhost/` returning 404 afterwards is normal: the controller has no
> default backend and only serves the named hosts.

### First-run secrets: none

Nothing to set up. On first run the AppHost generates `sql-password`, `jenkins-password` and
`nexus-password` into its user-secrets and reuses them on every later run. `JenkinsUrl` and
`JenkinsApiToken` no longer exist as parameters — the URL comes from the `jenkins` container's
endpoint, and `jenkins-password` doubles as the API token (Jenkins accepts the admin password for
Basic auth, which is what `JenkinsClient` uses).

To see the generated values — the Jenkins and Nexus UIs both log in as `admin` with them:

```bash
dotnet user-secrets list --project src/Aspire/Cicd.Aspire.Host
```

> These are generated by `SecretStore.GetOrCreate` rather than Aspire's `GenerateParameterDefault`,
> which regenerates on every run even with `persist: true`. A drifting value is fatal here: SQL
> Server and Nexus both bake their credential into a data volume on first init and never update it,
> so the next run cannot log in. If you ever *do* need to reset one, delete the matching volume too
> (`docker volume rm nexus-data`, or the `*-sql-data` volume).

**Optional — AI features.** The one credential the platform can't generate for itself is an
Anthropic API key. Without it the app runs normally and AI actions simply don't appear; with it you
get "Explain this CVE" on the SBOM pages and the AI half of the usage ledger. See [ai.md](ai.md).

```bash
dotnet user-secrets set Parameters:AiApiKey <key> --project src/Aspire/Cicd.Aspire.Host
```

Remaining manual step, once per machine: start the **`build-agent-image`** resource from the
dashboard. It builds `netsdk10:latest` (~5 min), which every `cicd-*` job runs its stages in.
`nexus-provision` warns on every start until it exists.

- Nexus / crane / aspirate / kubeconfig parameters have sensible fallbacks — override only when used
  (see the runbooks below). Note `deployment-api` fails startup if `crane` and `aspirate` are not on
  PATH; see [deployment/prerequisites.md](deployment/prerequisites.md).

### Changing the Jenkins controller config

`jenkins` and `nexus` use `ContainerLifetime.Persistent`, so Aspire **reuses an existing container by
name**. Aspire will rebuild the image after you edit `jenkins/controller/plugins.txt`,
`jenkins/controller/casc/jenkins.yaml` or `jenkins/jobs/cicd-jobs.groovy`, but the running container
is not replaced — remove it first:

```bash
docker rm -f jenkins   # jenkins-home volume (jobs, build history) is untouched
```

Databases auto-migrate on startup (`Database__AutoMigrate=true`); **never commit secrets** — use
environment variables / user-secrets.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/Aspire/Cicd.Aspire.Host` | Aspire orchestration (the run entry point) |
| `src/jenkins/` | CI service (Domain / Application / Infrastructure / Api / Client / Orchestrator) |
| `src/deployment/` | Deployment service (Domain / Application / Infrastructure / Api / Contracts) |
| `src/web-admin/` | Blazor Server admin UI |
| `src/shared/Cicd.IntegrationEvents` | Cross-service event contracts |
| `jenkins/` | Jenkinsfiles (build / scan / publish) |
| `samples/aspire-sample/` | Sample Aspire app + `publish-to-nexus.sh` |
| `docs/` | Documentation |

## Common commands

```bash
dotnet run --project src/Aspire/Cicd.Aspire.Host          # run the whole stack
dotnet build src/deployment/deployment.sln                # build the deployment service
dotnet test                                                # run tests
# EF migration (deployment service):
dotnet ef migrations add <Name> --project src/deployment/Deployment.Infrastructure --startup-project src/deployment/Deployment.Api
```

## Deeper setup & runbooks

| Doc | What |
| --- | --- |
| [deployment/prerequisites.md](deployment/prerequisites.md) | GCP / crane / cluster prerequisites |
| [deployment/aspire-k8s-local-runbook.md](deployment/aspire-k8s-local-runbook.md) | Local docker-desktop cluster + Nexus setup for Aspire deploys |
| [demos/](demos/) | Live demo runbooks — blue-green, build pipeline, webhooks/ngrok, Kubernetes admin screens |
| [build-sync.md](build-sync.md) | How CI build/artifact reconciliation works |
| [sbom-setup.md](sbom-setup.md) | SBOM generation + Nexus storage |
