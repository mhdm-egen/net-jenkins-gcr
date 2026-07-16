# CI/CD Platform

A repo-agnostic CI/CD platform: **Jenkins** builds any Git repo and publishes artifacts to
**Sonatype Nexus**; an event-driven **deployment service** promotes them to **Google Cloud Run**
(per-service) or deploys whole **.NET Aspire** apps to **Kubernetes** (via Aspir8). The .NET services,
SQL Server, and RabbitMQ are orchestrated locally by **.NET Aspire**.

- **Stack:** .NET 10 · C# 13 · ASP.NET Core · EF Core 10 · Clean Architecture · Wolverine (CQRS + bus +
  SQL outbox) · Blazor Server + MudBlazor · SQL Server · RabbitMQ.
- **Targets:** Google Artifact Registry + Cloud Run, and Kubernetes (Aspir8).

## Service map

| Resource | Project | Role |
| --- | --- | --- |
| `jenkins-api` | `src/jenkins/Jenkins.*` | Orchestrates Jenkins jobs, polls builds, reconciles Nexus artifacts, raises CI events |
| `deployment-api` | `src/deployment/Deployment.*` | Services × Environments × Mappings, container inventory, deploy runs; Cloud Run + Aspire→K8s |
| `web-admin` | `src/web-admin/cicd.web.admin` | Blazor UI over both APIs (Jenkins, Nexus, SCA/SBOM, Deployment) |
| `sql` | SQL Server (container) | `JenkinsCi` + `Deployment` databases |
| `messaging` | RabbitMQ (container) | `ci.events` / `deployment.events` fanout channels |
| Jenkins + Nexus | standalone containers | pipeline execution + artifact storage (external to the AppHost) |

See [docs/architecture.md](docs/architecture.md) for the full picture and diagrams.

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

### First-run secrets (once per machine)

Three AppHost parameters have no fallback — set them via user-secrets or `dotnet run` will block on a
prompt:

```bash
dotnet user-secrets set "Parameters:sql-password"     "<a-strong-Passw0rd!>" --project src/Aspire/Cicd.Aspire.Host
dotnet user-secrets set "Parameters:JenkinsUrl"       "http://localhost:8080" --project src/Aspire/Cicd.Aspire.Host
dotnet user-secrets set "Parameters:JenkinsApiToken"  "<token-or-placeholder>" --project src/Aspire/Cicd.Aspire.Host
```

- `sql-password` — SQL Server bakes this into its data volume on first init; pick one and keep it stable.
- `JenkinsUrl` / `JenkinsApiToken` — only exercised by CI build-sync; a placeholder is fine for
  deployment-only work.
- Nexus / crane / aspirate / kubeconfig parameters have sensible fallbacks — override only when used
  (see the runbooks below).

Databases auto-migrate on startup (`Database__AutoMigrate=true`); **never commit secrets** — use
environment variables / user-secrets.

## Documentation

| Doc | What |
| --- | --- |
| [docs/features.md](docs/features.md) | Complete feature catalog — everything the platform does, by area, with PR refs |
| [docs/architecture.md](docs/architecture.md) | System overview, CI pipeline, event-driven deploy, components |
| [docs/deployment/deploy-safety-features.md](docs/deployment/deploy-safety-features.md) | Notifications, rollback, promotion, approval gate, blue-green, live-status & drift, namespace pinning |
| [docs/deployment/preview-environments.md](docs/deployment/preview-environments.md) | Per-PR previews, CI branch routing, the teardown webhook + git wiring |
| [docs/demos/](docs/demos/) | Live demo runbooks — blue-green, build pipeline, webhooks/ngrok, Kubernetes admin screens |
| [docs/deployment/aspire-k8s-local-runbook.md](docs/deployment/aspire-k8s-local-runbook.md) | Local docker-desktop cluster + Nexus setup for Aspire deploys |
| [docs/deployment/prerequisites.md](docs/deployment/prerequisites.md) | GCP / crane / cluster prerequisites |
| [docs/deployment/deployment-model-decisions.md](docs/deployment/deployment-model-decisions.md) | Deployment model design decisions |
| [docs/build-sync.md](docs/build-sync.md) | How CI build/artifact reconciliation works |
| [docs/sbom-setup.md](docs/sbom-setup.md) | SBOM generation + Nexus storage |
| [docs/ci/ci-model-decisions.md](docs/ci/ci-model-decisions.md) | CI model design decisions |

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
