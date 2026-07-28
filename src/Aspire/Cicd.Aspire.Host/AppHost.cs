using Aspire.Hosting.ApplicationModel;
using Cicd.Aspire.Host;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Fixed docker network shared with the sibling build-agent containers Jenkins launches through the
// docker socket. Must exist before any container starts — see DockerNetwork.cs for the full why.
const string CicdNetwork = "cicd-net";
DockerNetwork.EnsureExists(CicdNetwork);

// --- Secrets -------------------------------------------------------------------------------------
// Generated once into this AppHost's user-secrets and reused forever after, so a headless
// `dotnet run` never blocks on an interactive prompt and no secret is ever committed.
//
// These are passed to AddParameter as EAGER fixed strings rather than using Aspire's own
// GenerateParameterDefault, which regenerates on every run even with persist: true — see
// SecretStore.cs for the evidence and why that is fatal for sql and nexus specifically.
const string UserSecretsId = "7e3b1a2c-9d4f-4a6b-8c1e-2f5a9b0c3d4e";

// Jenkins admin password. Doubles as the REST API token: JenkinsClient authenticates with plain
// Basic base64(User:ApiToken) (src/jenkins/Jenkins.Client/JenkinsClient.cs:32) and Jenkins accepts
// the admin password in that slot across the REST surface, including the crumb issuer. One secret,
// and no chicken-and-egg with JCasC — which creates the admin user from this same value.
var jenkinsPassword = builder.AddParameter(
    "jenkins-password", SecretStore.GetOrCreate(UserSecretsId, "jenkins-password"), secret: true);

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

// Nexus admin password — generated + persisted like the others. Nexus bakes credentials into
// /nexus-data on first init, so this must stay stable across runs; `nexus-provision` rotates the
// image's deterministic first-run password to this value.
var nexusPassword = builder.AddParameter(
    "nexus-password", SecretStore.GetOrCreate(UserSecretsId, "nexus-password"), secret: true);

// Plain (non-parameter) strings — no dashboard rows, no prompts.
var nexusUsername = NexusParam("NexusUsername", "admin");
var nexusDockerRepo = NexusParam("NexusDockerRepository", "docker-private");
// The registry host recorded in image pull references, consumed by build agents and cluster nodes —
// NOT dialled by the Aspire-run host processes, which use the endpoint URLs below.
var nexusDockerHost = NexusParam("NexusDockerHost", "nexus:8082");
// Which repo/branch the seeded cicd-* jobs should fetch their Jenkinsfiles from.
var pipelineRepoUrl = NexusParam("PipelineRepoUrl", "https://github.com/mhdm-egen/net-jenkins-gcr.git");
var pipelineRepoBranch = NexusParam("PipelineRepoBranch", "main");

// Anthropic API key for the admin UI's AI features. Empty => AI is disabled (the AiClient
// soft-fails and the UI surfaces a banner — nothing else breaks). Set via user-secrets:
//   dotnet user-secrets set Parameters:AiApiKey <key>
var aiApiKey = builder.AddParameter("AiApiKey", NexusParam("AiApiKey", ""), secret: true);

// SQL Server (container) + the Jenkins CI database. The sa password is PERSISTED rather than
// re-generated per run — SQL Server bakes it into the data volume on first init and never updates
// it, so a drifting value leaves the volume's sa password mismatched ("Login failed for user 'sa'").
var sqlPassword = builder.AddParameter(
    "sql-password", SecretStore.GetOrCreate(UserSecretsId, "sql-password"), secret: true);
var sql = builder.AddSqlServer("sql", password: sqlPassword).WithDataVolume();
var jenkinsDb = sql.AddDatabase("JenkinsCi");
var deploymentDb = sql.AddDatabase("Deployment");
var meteringDb = sql.AddDatabase("Metering");

// RabbitMQ broker for the CI service's outbox/event publishing. Ephemeral (no data volume) —
// Wolverine's per-service SQL outbox provides durability, so the broker itself is disposable.
var rabbit = builder.AddRabbitMQ("messaging").WithManagementPlugin();

// Redis — distributed cache for the admin UI's AI layer (cached CVE/DORA insights) and,
// later, the metering service's gauge snapshots. Ephemeral: the cache is rebuildable.
var redis = builder.AddRedis("redis");

// Nexus — artifact store. 8081 = UI/REST/nuget/raw, 8082 = the docker registry connector (plain
// HTTP), which `nexus-provision` turns on. Pinned everywhere on purpose:
//   * WithContainerName("nexus")  — the literal hostname is load-bearing. It is recorded in image
//     pull references and the .NET SDK rejects single-label registry hosts, hence "nexus:8082".
//   * Persistent lifetime         — ~2 min cold boot; survives AppHost restarts. NOTE this also
//     means Aspire will ADOPT a pre-existing container of the same name without re-applying config;
//     `docker rm -f nexus` after changing anything here.
//   * isProxied: false            — publish the ports straight through (-p 8081:8081), matching the
//     old devops/docker-runs.sh and keeping registry blob traffic off the DCP proxy.
// The tag is pinned deliberately: recent :latest is Community Edition, which adds an EULA gate.
var nexus = builder.AddContainer("nexus", "sonatype/nexus3", "3.70.1")
    .WithContainerName("nexus")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithVolume("nexus-data", "/nexus-data")
    .WithHttpEndpoint(targetPort: 8081, port: 8081, name: "http", isProxied: false)
    .WithEndpoint(targetPort: 8082, port: 8082, name: "registry", scheme: "http", isProxied: false)
    .WithEnvironment("INSTALL4J_ADD_VM_PARAMS", "-Xms1g -Xmx1g -XX:MaxDirectMemorySize=2g")
    // Deterministic first-run admin password so the provisioner can bootstrap headlessly; it is
    // rotated to the generated nexus-password on first provision.
    .WithEnvironment("NEXUS_SECURITY_RANDOMPASSWORD", "false")
    .WithHttpHealthCheck("/service/rest/v1/status", 200, "http");

// Attach Nexus to cicd-net AFTER it starts.
//
// Passing a second "--network" through WithContainerRuntimeArgs does NOT work — verified: the
// container came up attached only to "aspire-persistent-network-<hash>-Cicd.Aspire.Host", and a
// probe container on cicd-net failed with "Could not resolve host: nexus". DCP owns the network
// assignment at `docker run` time, so the reliable route is to connect once the container exists.
// Docker registers the container name as a DNS name on every user-defined network it joins, so this
// is what makes "nexus:8081"/"nexus:8082" resolvable from the Jenkins build agents.
nexus.OnResourceReady((_, _, _) =>
{
    DockerNetwork.Connect(CicdNetwork, "nexus");
    return Task.CompletedTask;
});

// Jenkins controller, built from jenkins/controller/Dockerfile with the repo's `jenkins/` directory
// as the build context — so the EXISTING jenkins/jobs/cicd-jobs.groovy is baked in and seeded by
// JCasC on boot. Everything the controller needs (plugins, admin user, credentials, the five cicd-*
// jobs) is declared in jenkins/controller/casc/jenkins.yaml; nothing is configured by hand.
//
// Persistent + WithContainerName for the same reasons as Nexus. NOTE the consequence: Aspire
// rebuilds the image but REUSES an existing container by name, so an edit to plugins.txt or
// casc/jenkins.yaml does nothing until you `docker rm -f jenkins`.
var jenkinsController = builder.AddDockerfile("jenkins", "../../../jenkins", "controller/Dockerfile")
    .WithContainerName("jenkins")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithVolume("jenkins-home", "/var/jenkins_home")
    // Raw runtime args rather than WithBindMount: WithBindMount treats "/var/run/docker.sock" as a
    // rooted path and would rewrite it to C:\var\run\docker.sock on Windows. --group-add 0 gives the
    // non-root `jenkins` user access to the root-owned socket without running the controller as root.
    .WithContainerRuntimeArgs("-v", "/var/run/docker.sock:/var/run/docker.sock", "--group-add", "0")
    .WithHttpEndpoint(targetPort: 8080, port: 8080, name: "http", isProxied: false)
    .WithEnvironment("JENKINS_ADMIN_ID", nexusUsername)
    .WithEnvironment("JENKINS_ADMIN_PASSWORD", jenkinsPassword)
    .WithEnvironment("JENKINS_LOCATION_URL", "http://localhost:8080/")
    .WithEnvironment("NEXUS_USERNAME", nexusUsername)
    .WithEnvironment("NEXUS_PASSWORD", nexusPassword)
    // Consumed by the cfg() env rung in jenkins/jobs/cicd-jobs.groovy so the seeded jobs point at
    // THIS checkout rather than the hard-coded upstream default.
    .WithEnvironment("PIPELINE_REPO_URL", pipelineRepoUrl)
    .WithEnvironment("PIPELINE_REPO_BRANCH", pipelineRepoBranch)
    .WithHttpHealthCheck("/login", 200, "http");

// Same story as Nexus: the build agents Jenkins launches live on cicd-net, so the controller has to
// be reachable there too. See DockerNetwork.cs.
jenkinsController.OnResourceReady((_, _, _) =>
{
    DockerNetwork.Connect(CicdNetwork, "jenkins");
    return Task.CompletedTask;
});

// One-shot Nexus provisioner: creates the four repositories, turns on the :8082 docker connector,
// activates the DockerToken + NuGetApiKey realms, and rotates the image's first-run admin password
// to the generated one. Idempotent — it runs to completion on every start and no-ops thereafter.
// Replaces the Nexus setup wizard, four hand-clicked repositories, and the curl in docs/sbom-setup.md.
var nexusProvision = builder.AddProject<Projects.Util_Cli>("nexus-provision")
    .WithArgs("nexus-provision")
    .WithEnvironment("NEXUS_URL", nexus.GetEndpoint("http"))
    .WithEnvironment("NEXUS_USER", nexusUsername)
    .WithEnvironment("NEXUS_PASS", nexusPassword)
    .WithEnvironment("NEXUS_DOCKER_REPO", nexusDockerRepo)
    .WaitFor(nexus);

var jenkins = builder.AddProject<Projects.Jenkins_Api>("jenkins-api")
    // Pin the http endpoint's (proxy) port so the inbound git-webhook URL is stable across restarts —
    // otherwise Aspire assigns a fresh port each run and any ngrok tunnel / provider config drifts.
    // See docs/demos/build-pipeline-demo.md → "Webhooks locally".
    .WithEndpoint("http", e => e.Port = 7229, createIfNotExists: false)
    .WithReference(jenkinsDb)
    .WaitFor(sql)
    .WithReference(rabbit)
    .WaitFor(rabbit)
    .WaitFor(nexus)
    .WaitFor(jenkinsController)
    // The artifact-reconcile loop polls Nexus as soon as it starts — gate on the repositories existing.
    .WaitForCompletion(nexusProvision)
    .WithEnvironment("Database__AutoMigrate", "true")
    .WithEnvironment("Jenkins__ApiToken", jenkinsPassword)
    .WithEnvironment("Jenkins__Url", jenkinsController.GetEndpoint("http"))
    // Nexus reconcile (option b): attaches each build's pushed docker image as a publication.
    // Url is the HOST-process view (localhost) — this service runs as a process, not a container,
    // so it cannot resolve the "nexus" hostname. DockerRegistryHost stays "nexus:8082" because it
    // is a recorded pull reference for agents/clusters, not an address this service dials.
    .WithEnvironment("Nexus__Url", nexus.GetEndpoint("http"))
    .WithEnvironment("Nexus__User", nexusUsername)
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

var deployment = builder.AddProject<Projects.Deployment_Api>("deployment-api")
    // Pin the http endpoint's (proxy) port so Aspire binds + injects a reachable URL. Without this, an
    // unpinned endpoint injects the launchSettings port (9601) into web-admin while the proxy runs elsewhere,
    // so the admin UI's deployment client can't reach the API. (Mirrors the jenkins-api pin above.)
    .WithEndpoint("http", e => e.Port = 7228, createIfNotExists: false)
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

// Metering & cost service — general usage ledger; the AI-tokens meter ingests via HTTP
// from web-admin (build/deploy bus meters come later). Own SQL database.
var metering = builder.AddProject<Projects.Metering_Api>("metering-api")
    .WithEndpoint("http", e => e.Port = 7230, createIfNotExists: false)
    .WithReference(meteringDb)
    .WaitFor(sql)
    .WithReference(rabbit)   // subscribe to ci.events / deployment.events for build/deploy meters
    .WaitFor(rabbit)
    .WithEnvironment("Database__AutoMigrate", "true");

builder.AddProject<Projects.cicd_web_admin>("web-admin")
    .WithReference(jenkins)
    .WaitFor(jenkins)
    .WithReference(deployment)
    .WaitFor(deployment)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(metering)
    .WaitFor(metering)
    .WithEnvironment("JenkinsApi__BaseUrl", jenkins.GetEndpoint("http"))
    .WithEnvironment("Deployment__Api__BaseUrl", deployment.GetEndpoint("http"))
    .WithEnvironment("Metering__Api__BaseUrl", metering.GetEndpoint("http"))
    // Anthropic API key (empty => AI features disabled, app still runs).
    .WithEnvironment("Ai__ApiKey", aiApiKey)
    .WithEnvironment("Jenkins__ApiToken", jenkinsPassword)
    .WithEnvironment("Jenkins__Url", jenkinsController.GetEndpoint("http"))
    // Nexus config for the admin UI's Docker/NuGet pages. Url is the host-process view; see the
    // jenkins-api block above for why DockerRegistryHost stays "nexus:8082".
    .WithEnvironment("Nexus__Url", nexus.GetEndpoint("http"))
    .WithEnvironment("Nexus__User", nexusUsername)
    .WithEnvironment("Nexus__Password", nexusPassword)
    .WithEnvironment("Nexus__DockerRegistryHost", nexusDockerHost)
    .WithEnvironment("Nexus__DockerHostedRepository", nexusDockerRepo);

builder.Build().Run();
