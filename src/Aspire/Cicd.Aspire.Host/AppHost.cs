using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Secrets / parameters — set via the AppHost's user-secrets:
//   dotnet user-secrets set Parameters:JenkinsApiToken <token>
//   dotnet user-secrets set Parameters:JenkinsUrl http://<jenkins>:8080
var jenkinsToken = builder.AddParameter("JenkinsApiToken", secret: true);
var jenkinsUrl = builder.AddParameter("JenkinsUrl");

// Nexus — required by the CI service's artifact-reconcile loop (JenkinsBuildSyncService): it polls
// Nexus for each tracked build's pushed docker image and attaches the publication. Also used by
// the admin UI's Docker/NuGet pages. The reconcile reader is only registered when BOTH Url and
// Password are non-empty. These must be EAGER (fixed value) — a lazy secret AddParameter that
// Aspire can't auto-resolve becomes an interactive prompt that blocks the referencing resource in
// a headless `dotnet run`; builder.Configuration does NOT surface the parameter user-secrets, so we
// read them explicitly. Override via the AppHost's user-secrets:
//   dotnet user-secrets set Parameters:NexusUrl http://<nexus>:8081
//   dotnet user-secrets set Parameters:NexusPassword <password>
//   dotnet user-secrets set Parameters:NexusDockerHost <nexus>:8082
var paramSecrets = new ConfigurationBuilder()
    .AddUserSecrets("7e3b1a2c-9d4f-4a6b-8c1e-2f5a9b0c3d4e")
    .Build();
string NexusParam(string key, string fallback) =>
    builder.Configuration[$"Parameters:{key}"] is { Length: > 0 } v ? v
    : paramSecrets[$"Parameters:{key}"] is { Length: > 0 } s ? s
    : fallback;

var nexusUrl = builder.AddParameter("NexusUrl", NexusParam("NexusUrl", "http://nexus:8081"));
var nexusPassword = builder.AddParameter("NexusPassword", NexusParam("NexusPassword", ""), secret: true);
var nexusDockerHost = builder.AddParameter("NexusDockerHost", NexusParam("NexusDockerHost", "nexus:8082"));
var nexusDockerRepo = builder.AddParameter("NexusDockerRepository", NexusParam("NexusDockerRepository", "docker-private"));

// SQL Server (container) + the Jenkins CI database. The sa password is an EXPLICIT, pinned
// parameter (Parameters:sql-password in user-secrets) rather than Aspire's auto-generated one —
// SQL Server bakes it into the data volume on first init and never updates it, so a drifting
// auto-generated value leaves the volume's sa password mismatched ("Login failed for user 'sa'").
var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sql = builder.AddSqlServer("sql", password: sqlPassword).WithDataVolume();
var jenkinsDb = sql.AddDatabase("JenkinsCi");
var deploymentDb = sql.AddDatabase("Deployment");

// RabbitMQ broker for the CI service's outbox/event publishing. Ephemeral (no data volume) —
// Wolverine's per-service SQL outbox provides durability, so the broker itself is disposable.
var rabbit = builder.AddRabbitMQ("messaging").WithManagementPlugin();

var jenkins = builder.AddProject<Projects.Jenkins_Api>("jenkins-api")
    // Pin the http endpoint's (proxy) port so the inbound git-webhook URL is stable across restarts —
    // otherwise Aspire assigns a fresh port each run and any ngrok tunnel / provider config drifts.
    // See docs/demos/build-pipeline-demo.md → "Webhooks locally".
    .WithEndpoint("http", e => e.Port = 7229, createIfNotExists: false)
    .WithReference(jenkinsDb)
    .WaitFor(sql)
    .WithReference(rabbit)
    .WaitFor(rabbit)
    .WithEnvironment("Database__AutoMigrate", "true")
    .WithEnvironment("Jenkins__ApiToken", jenkinsToken)
    .WithEnvironment("Jenkins__Url", jenkinsUrl)
    // Nexus reconcile (option b): attaches each build's pushed docker image as a publication.
    .WithEnvironment("Nexus__Url", nexusUrl)
    .WithEnvironment("Nexus__Password", nexusPassword)
    .WithEnvironment("Nexus__DockerRegistryHost", nexusDockerHost)
    .WithEnvironment("Nexus__DockerRepository", nexusDockerRepo);

// Deployment: maps services↔environments↔containers and runs deployments (promote Nexus→GAR,
// then create/update Cloud Run). Consumes the CI ContainerPublished event for auto-deploy. GCP
// auth is ambient ADC (gcloud / GOOGLE_APPLICATION_CREDENTIALS); crane must reach Nexus + GAR.
// GarPush shells out to go-containerregistry `crane` (NOT the similarly-named michaelsauter/crane
// that ships on chocolatey's PATH). Point this at the real crane.exe; defaults to "crane" on PATH.
// Override via the AppHost's user-secrets:
//   dotnet user-secrets set Parameters:CraneExecutable <path-to-go-containerregistry-crane.exe>
var craneExecutable = NexusParam("CraneExecutable", "crane");
// Make Cloud Run services publicly reachable (allUsers run.invoker) on deploy. Default true for local
// dev; set Parameters:CloudRunAllowUnauthenticated=false to keep them private. Org policy that forbids
// allUsers is handled gracefully (the service stays private, the deploy still succeeds).
var cloudRunAllowUnauthenticated = NexusParam("CloudRunAllowUnauthenticated", "true");

// crane reads registry auth from $DOCKER_CONFIG/config.json. The host's default ~/.docker config
// uses Docker Desktop's locked "desktop" credsStore (crane can't write it), so we hand crane an
// isolated, inline-auth config dir pre-seeded via `crane auth login` for Nexus (and later the GAR
// gcloud credHelper). Empty => crane uses its default. Override via the AppHost's user-secrets:
//   dotnet user-secrets set Parameters:DockerConfigDir <dir-containing-config.json>
var dockerConfigDir = NexusParam("DockerConfigDir", "");

// Aspir8 (aspirate) deploys a whole Aspire app to Kubernetes. Executable defaults to "aspirate" on
// PATH (dotnet tool install -g Aspirate). PullRegistry is the registry the CLUSTER pulls from — set it
// when the build/push host isn't node-reachable (local: host.docker.internal:8082 vs localhost:8082);
// empty deploys the manifests as generated. Override via the AppHost's user-secrets:
//   dotnet user-secrets set Parameters:AspiratePullRegistry host.docker.internal:8082
var aspirateExecutable = NexusParam("AspirateExecutable", "aspirate");
var aspiratePullRegistry = NexusParam("AspiratePullRegistry", "");
// Optional kubeconfig for non-default/remote clusters; empty => the service's default ~/.kube/config.
var aspirateKubeconfig = NexusParam("AspirateKubeconfig", "");

// Nexus docker-v2 endpoint the deployment SERVICE can reach (e.g. http://localhost:8082) to resolve
// image digests for provenance-pinning Aspire deploys. Empty => digest-pinning disabled (floating tag).
//   dotnet user-secrets set Parameters:NexusRegistryV2Url http://localhost:8082
var nexusRegistryV2Url = NexusParam("NexusRegistryV2Url", "");
var nexusUsername = NexusParam("NexusUsername", "admin");

var deployment = builder.AddProject<Projects.Deployment_Api>("deployment-api")
    .WithReference(deploymentDb)
    .WaitFor(sql)
    .WithReference(rabbit)
    .WaitFor(rabbit)
    .WithEnvironment("Database__AutoMigrate", "true")
    .WithEnvironment("Deployment__GoogleCloudRun__CraneExecutable", craneExecutable)
    .WithEnvironment("Deployment__GoogleCloudRun__AllowUnauthenticated", cloudRunAllowUnauthenticated)
    .WithEnvironment("Deployment__Aspirate__Executable", aspirateExecutable)
    .WithEnvironment("Deployment__Aspirate__PullRegistry", aspiratePullRegistry)
    .WithEnvironment("Deployment__Aspirate__Kubeconfig", aspirateKubeconfig)
    .WithEnvironment("Deployment__Nexus__RegistryV2Url", nexusRegistryV2Url)
    .WithEnvironment("Deployment__Nexus__Username", nexusUsername)
    .WithEnvironment("Deployment__Nexus__Password", nexusPassword);

if (dockerConfigDir.Length > 0)
    deployment = deployment.WithEnvironment("DOCKER_CONFIG", dockerConfigDir);

// Give the CI service the deployment API's URL so its git-webhook handler can call the preview
// teardown endpoint on PR close (jenkins-api → POST /api/deployment/previews/webhook).
jenkins.WithEnvironment("Deployment__ApiBaseUrl", deployment.GetEndpoint("http"));

builder.AddProject<Projects.cicd_web_admin>("web-admin")
    .WithReference(jenkins)
    .WaitFor(jenkins)
    .WithReference(deployment)
    .WaitFor(deployment)
    .WithEnvironment("JenkinsApi__BaseUrl", jenkins.GetEndpoint("http"))
    .WithEnvironment("Deployment__Api__BaseUrl", deployment.GetEndpoint("http"))
    .WithEnvironment("Jenkins__ApiToken", jenkinsToken)
    .WithEnvironment("Jenkins__Url", jenkinsUrl)
    // Nexus config for the admin UI's Docker/NuGet pages.
    .WithEnvironment("Nexus__Url", nexusUrl)
    .WithEnvironment("Nexus__Password", nexusPassword)
    .WithEnvironment("Nexus__DockerRegistryHost", nexusDockerHost)
    .WithEnvironment("Nexus__DockerHostedRepository", nexusDockerRepo);

builder.Build().Run();
