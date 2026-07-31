# Deployment service — prerequisites & auth setup

The deployment service (`deployment-api`) promotes a container from Nexus into Google Artifact
Registry (GAR) and then deploys it to Cloud Run. The **GarPush** step shells out to the `crane` CLI;
the **CloudRunDeploy** step uses the Google.Cloud.Run.V2 admin API. Both need external tooling and
ambient credentials that are *not* checked into the repo. If any of the below is missing, the run
fails at the relevant step — and, since the change adding options validation, a missing/unresolvable
`crane` now fails service **startup** with a clear message instead of failing the first deploy.

## 1. Install crane (go-containerregistry)

Use go-containerregistry's `crane` — **not** the similarly-named `michaelsauter/crane` that some
package managers ship.

- Download `go-containerregistry_Windows_x86_64.tar.gz` from
  <https://github.com/google/go-containerregistry/releases> and extract `crane.exe`, or
- `go install github.com/google/go-containerregistry/cmd/crane@latest`

Verify: `crane version`.

## 2. Point the AppHost at crane + an isolated docker config

The stack runs under .NET Aspire (`src/Aspire/Cicd.Aspire.Host`). The AppHost injects
`Deployment__GoogleCloudRun__CraneExecutable` and (optionally) `DOCKER_CONFIG` into `deployment-api`
from its **user-secrets** — this env var outranks `appsettings*.json`, so configure it here, not in
appsettings.

`DockerConfigDir` is **a directory you create** that will hold crane's `config.json` — the file
crane reads to get registry credentials (same format as `~/.docker/config.json`). You don't
hand-write that file; the `crane auth login` / `gcloud auth configure-docker` commands in step 3
populate it. Use a **dedicated, empty** dir rather than the default `~/.docker`, because the default
config uses Docker Desktop's locked `desktop` credential store, which crane can't use for these
registries. The name is arbitrary (`.cicd-docker` below is just an example). Leave `DockerConfigDir`
unset to fall back to crane's default `~/.docker` config.

> **Pointing `DockerConfigDir` at `~/.docker` looks like it works and doesn't.** A `docker login`
> stores the credential in Docker Desktop's `desktop` credsStore and leaves an *empty* `"auths"`
> entry behind, so crane reads no credential and sends an unauthenticated request. The symptom is a
> GarPush failure with `401 Unauthorized` on the **source**, which reads like a Nexus problem rather
> than a config-file one. A correct `config.json` has an inline `"auth"` value for Nexus and a
> `"credHelpers"` entry for GAR, and **no** `credsStore`.

```powershell
# Create the (empty) dir that will hold crane's config.json — any path/name works.
mkdir C:\Users\<you>\.cicd-docker

dotnet user-secrets --project src/Aspire/Cicd.Aspire.Host set Parameters:CraneExecutable "C:\path\to\crane.exe"
dotnet user-secrets --project src/Aspire/Cicd.Aspire.Host set Parameters:DockerConfigDir "C:\Users\<you>\.cicd-docker"
```

## 3. Seed registry auth (into `DockerConfigDir`)

Point `DOCKER_CONFIG` at that same dir so the commands below **write `config.json` into it** (these
are one-time):

```powershell
$env:DOCKER_CONFIG = "C:\Users\<you>\.cicd-docker"

# Nexus (Docker) — for pulling the source image.
# Log in to the host crane actually DIALS, which is not necessarily the host recorded in the image
# reference. Under the Aspire host that is localhost:8082 (see Deployment:Nexus:DockerRegistryDialHost);
# under docker-compose, deployment-api is itself a container and dials nexus:8082.
crane auth login localhost:8082 -u <user> -p <password>

# GAR — register the gcloud credential helper for each region you deploy to
gcloud auth configure-docker us-west1-docker.pkg.dev      # add us-central1-docker.pkg.dev, etc. as needed
```

Verify a region resolves: `crane auth get us-west1-docker.pkg.dev` should print a credential JSON,
not `Reauthentication failed…`.

## 4. Application Default Credentials (Cloud Run)

The Cloud Run deployer authenticates via ADC — no credential is configured in code. Provide one of:

- `gcloud auth application-default login` (local dev), or
- `GOOGLE_APPLICATION_CREDENTIALS` pointing at a service-account key file, or
- Workload Identity (when running in GCP).

If the gcloud helper used for GAR (step 3) reports `Reauthentication failed. cannot prompt during
non-interactive execution`, refresh with `gcloud auth login` **and**
`gcloud auth application-default login`, then restart `deployment-api`.

## 5. Configure the deployment target

Region, GCP project and GAR repository live on the **environment**; the Cloud Run service name lives
on the **mapping**. Set these via the deployment API / admin UI before triggering a run — there is no
default seeding. The run snapshots them at request time.

## Troubleshooting

Failures are categorized (`StepFailureKind`) and shown in the completion toast and on the run's step
detail (`GET /api/deployment/runs/{id}`):

| Toast says…            | Category           | Likely fix                                                        |
|------------------------|--------------------|-------------------------------------------------------------------|
| tooling missing        | `ToolMissing`      | Install crane / fix `Parameters:CraneExecutable` (step 1–2)       |
| registry auth          | `RegistryAuth`     | Re-run `crane auth login` / `configure-docker` (step 3)           |
| registry error         | `RegistryError`    | Check the source ref and GAR repo exist; inspect step detail      |
| Cloud Run auth         | `CloudRunAuth`     | Refresh ADC (step 4); check the SA's Cloud Run IAM roles          |
| service not found      | `CloudRunNotFound` | Create the service or enable `CreateServiceIfMissing`             |
| timed out              | `Timeout`          | Revision didn't become ready; check Cloud Run logs / raise timeout|
| configuration          | `Config`           | Missing region/project/GAR repo/service name (step 5)             |
