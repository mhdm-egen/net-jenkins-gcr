using Jenkins.Client;
using Jenkins.Orchestrator;
using Cicd.Web.Admin.Components;
using Cicd.Web.Admin.Services;
using Cicd.Web.Admin.Services.Builds;
using Cicd.Web.Admin.Services.Ci;
using Cicd.Web.Admin.Services.Ai;
using Cicd.Web.Admin.Services.Metering;
using Cicd.Web.Admin.Services.Gcp;
using Cicd.Web.Admin.Services.Nexus;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Per-circuit theme state (shared by MainLayout's provider + the Settings page's selector).
builder.Services.AddScoped<Cicd.Web.Admin.Services.ThemeService>();

// Per-circuit owner of the run-completion SignalR hubs (toasts, live table refresh, pending badge).
builder.Services.AddScoped<Cicd.Web.Admin.Services.RunEventsService>();

// Per-circuit drawer nav expansion state (collapse-by-default + remember, persisted to localStorage).
builder.Services.AddScoped<Cicd.Web.Admin.Services.NavStateService>();

// Jenkins config — read once at startup from configuration / env vars.
// Required: Jenkins:ApiToken (env: Jenkins__ApiToken)
var jenkinsApiToken = builder.Configuration["Jenkins:ApiToken"];
if (string.IsNullOrEmpty(jenkinsApiToken))
    throw new InvalidOperationException(
        "Jenkins:ApiToken is not configured. Set env var Jenkins__ApiToken (double underscore) or set Jenkins:ApiToken in appsettings.");

var jenkinsOptions = new JenkinsOptions(
    BaseUrl:  builder.Configuration["Jenkins:Url"]  ?? "http://jenkins:8080",
    User:     builder.Configuration["Jenkins:User"] ?? "admin",
    ApiToken: jenkinsApiToken);

builder.Services.AddSingleton(jenkinsOptions);
builder.Services.AddSingleton<IJenkinsClient>(sp => new JenkinsClient(sp.GetRequiredService<JenkinsOptions>()));
builder.Services.AddSingleton<IPipelineOrchestrator, PipelineOrchestrator>();
builder.Services.AddSingleton<IPipelineRunner, PipelineRunner>();

// Build history reads come straight from Jenkins (live mode). Persisted build
// history is now owned by the Jenkins CI service (Jenkins.Api) — consumed by the
// CI pages — so the old local SQLite build mirror (BuildSync) has been retired.
builder.Services.AddSingleton<JenkinsLiveBuildStore>();
builder.Services.AddSingleton<IBuildStore>(sp => sp.GetRequiredService<JenkinsLiveBuildStore>());

// Health probe settings — configurable via Jenkins:Health:{PollIntervalSeconds,ProbeTimeoutSeconds}.
var healthOptions = builder.Configuration.GetSection("Jenkins:Health").Get<JenkinsHealthOptions>()
                    ?? new JenkinsHealthOptions();
if (healthOptions.PollIntervalSeconds <= 0)
    throw new InvalidOperationException("Jenkins:Health:PollIntervalSeconds must be > 0.");
if (healthOptions.ProbeTimeoutSeconds <= 0)
    throw new InvalidOperationException("Jenkins:Health:ProbeTimeoutSeconds must be > 0.");
builder.Services.AddSingleton(healthOptions);

// Same singleton serves three roles: the BackgroundService loop, the IJenkinsHealth
// snapshot exposed to UI components, and the concrete type (rarely needed but handy).
// It owns its own JenkinsClient (separate HttpClient from the orchestrator's) so the
// 30-second health pings don't share a connection pool with long-running build polls.
builder.Services.AddSingleton<JenkinsHealthService>();
builder.Services.AddSingleton<IJenkinsHealth>(sp => sp.GetRequiredService<JenkinsHealthService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<JenkinsHealthService>());

// Nexus — Url + repository name come from configuration; Password MUST come from
// env var (Nexus__Password) per project security policy. Missing creds don't fail
// startup; the Nuget page surfaces the configuration error.
var nexusOptions = builder.Configuration.GetSection("Nexus").Get<NexusOptions>() ?? new NexusOptions();
builder.Services.AddSingleton(nexusOptions);
builder.Services.AddSingleton<INexusClient, NexusClient>();

// Google Cloud — projects list comes from configuration; the GcpClient resolves
// Application Default Credentials at construction. If creds are missing, the
// client records the error but doesn't fail startup (the Google page surfaces it).
var gcpOptions = builder.Configuration.GetSection("Google").Get<GcpOptions>() ?? new GcpOptions();
builder.Services.AddSingleton(gcpOptions);
builder.Services.AddSingleton<IGcpClient, GcpClient>();

// Jenkins CI service (Jenkins.Api) — typed HttpClient. BaseUrl from config
// (JenkinsApi:BaseUrl); separate service from the direct Jenkins-server connection.
var jenkinsApiOptions = builder.Configuration.GetSection(JenkinsApiOptions.SectionName).Get<JenkinsApiOptions>()
                        ?? new JenkinsApiOptions();
builder.Services.AddSingleton(jenkinsApiOptions);
builder.Services.AddHttpClient<JenkinsApiClient>(c =>
{
    c.BaseAddress = new Uri(jenkinsApiOptions.BaseUrl.EndsWith('/')
        ? jenkinsApiOptions.BaseUrl
        : jenkinsApiOptions.BaseUrl + "/");
    // 2 min: normal calls are fast, but the admin CI-history reset prunes builds on the Jenkins server in a loop.
    c.Timeout = TimeSpan.FromSeconds(120);
});

// Deployment service (Deployment.Api) — typed HttpClient. BaseUrl from config (Deployment:Api:BaseUrl).
var deploymentApiOptions = builder.Configuration.GetSection(Cicd.Web.Admin.Services.Deployment.DeploymentApiOptions.SectionName)
                               .Get<Cicd.Web.Admin.Services.Deployment.DeploymentApiOptions>()
                           ?? new Cicd.Web.Admin.Services.Deployment.DeploymentApiOptions();
builder.Services.AddSingleton(deploymentApiOptions);
builder.Services.AddHttpClient<Cicd.Web.Admin.Services.Deployment.DeploymentApiClient>(c =>
{
    c.BaseAddress = new Uri(deploymentApiOptions.BaseUrl.EndsWith('/') ? deploymentApiOptions.BaseUrl : deploymentApiOptions.BaseUrl + "/");
    c.Timeout = TimeSpan.FromSeconds(60);
});

// AI layer — the model call runs on the official Anthropic SDK; token usage is captured
// at the SDK boundary and recorded via IAiUsageRecorder (Phase 0: OTel meter + log; a
// bus-publishing recorder arrives with the metering service). A missing Ai:ApiToken does
// NOT fail startup — AI features surface a banner and no-op (mirrors the Nexus pattern).
var aiOptions = builder.Configuration.GetSection(AiOptions.SectionName).Get<AiOptions>()
                ?? new AiOptions();
builder.Services.AddSingleton(aiOptions);

// Usage recorders — the local OTel meter always runs; the metering-api HTTP ingest runs
// when Metering:Api:BaseUrl is set (Aspire host / compose inject it). Fanned out via a
// composite so a metering outage never affects the AI call.
var meteringApiOptions = builder.Configuration.GetSection(MeteringApiOptions.SectionName).Get<MeteringApiOptions>()
                         ?? new MeteringApiOptions();
builder.Services.AddSingleton(meteringApiOptions);
if (!string.IsNullOrWhiteSpace(meteringApiOptions.BaseUrl))
    builder.Services.AddHttpClient(MeteringUsageRecorder.HttpClientName, c =>
        c.BaseAddress = new Uri(meteringApiOptions.BaseUrl.EndsWith('/') ? meteringApiOptions.BaseUrl : meteringApiOptions.BaseUrl + "/"));
builder.Services.AddSingleton<MeterAiUsageRecorder>();
builder.Services.AddSingleton<MeteringUsageRecorder>();
builder.Services.AddSingleton<IAiUsageRecorder>(sp => new CompositeAiUsageRecorder(
    sp.GetRequiredService<MeterAiUsageRecorder>(),
    sp.GetRequiredService<MeteringUsageRecorder>()));
builder.Services.AddSingleton<IAiInsightService, AiClient>();

// CVE-explain feature (Phase 1) — grounded, Redis-cached CVE explanations on the SBOM pages.
builder.Services.AddScoped<Cicd.Web.Admin.Services.Sca.ICveExplainer, Cicd.Web.Admin.Services.Sca.CveExplainer>();

// Export the AI usage meter through the OpenTelemetry pipeline set up by AddServiceDefaults.
builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter(MeterAiUsageRecorder.MeterName));

// Distributed cache for AI responses + gauge-collector snapshots. Redis when a connection
// string is present (ConnectionStrings:redis — injected by the Aspire host / docker-compose),
// otherwise an in-process fallback so web-admin still runs standalone.
var redisConnection = builder.Configuration.GetConnectionString("redis")
                      ?? builder.Configuration["Redis:Connection"];
if (!string.IsNullOrWhiteSpace(redisConnection))
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConnection);
else
    builder.Services.AddDistributedMemoryCache();

var app = builder.Build();

// Aspire defaults: /alive (liveness — also what the Dockerfile HEALTHCHECK hits) + /health.
app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
