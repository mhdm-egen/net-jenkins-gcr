using System.Text.Json.Serialization;
using Jenkins.Api.Endpoints;
using Jenkins.Application;
using Jenkins.Application.Features.Pipelines;
using Jenkins.Contracts.Seed;
using Jenkins.Infrastructure;
using Jenkins.Infrastructure.Persistence;
using Cicd.Messaging;
using Cicd.Notifications;
using Jenkins.Api.Hubs;
using Jenkins.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

// Enums on the wire are strings (BuildStatusDto = "Succeeded", not 2) — matches the
// deployment API's shape and the UI client's expectations.
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Application layer: FluentValidation registrations + use-case services.
builder.Services.AddJenkinsApplication();

// Infrastructure layer: EF Core (SQLite) DbContext + the deployment Releases client.
// ConnectionStrings:JenkinsCi + Deployment:ApiBaseUrl.
builder.Services.AddJenkinsInfrastructure(builder.Configuration);

// Build-sync background worker (Jenkins -> CI model). No-op when Jenkins is
// unconfigured. Jenkins:Url + Jenkins:ApiToken + Jenkins:Sync.
builder.Services.AddJenkinsBuildSync(builder.Configuration);

// Server-side pipeline-run execution (queue + executor + orchestrator) and the live
// SignalR stream (hub + notifier bridge from the Infrastructure executor).
builder.Services.AddJenkinsPipelineRuns(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IPipelineRunNotifier, PipelineRunNotifier>();

// Admin "danger zone" CI-history reset (mirror wipe + optional Jenkins-server build prune).
builder.Services.AddScoped<Jenkins.Api.Endpoints.ResetCiHistoryHandler>();

// Admin "demo setup" seed (registers demo repos + component mapping; additive + idempotent).
builder.Services.AddScoped<Jenkins.Api.Endpoints.SeedCiHandler>();

// CI notifications (Slack / email) — opt-in via Ci:Notifications. In-process notifiers on the
// build + pipeline-run domain events fan out through INotificationDispatcher.
//
// Section is Ci:, not Jenkins:. "Jenkins:" is the CONTROLLER CONNECTION (Url/ApiToken/Sync);
// this is the CI service's own outbound config, and Ci: is already its prefix (Ci:SeedDemoRepositories).
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Ci:Notifications"));
builder.Services.AddCicdNotifications();

// Wolverine: CQRS dispatcher + in-process bus. Handlers in Features/* are discovered
// by convention from the Application + Infrastructure assemblies. EF-transaction
// enrolment + a durable outbox are wired in when handlers land.
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Jenkins.Application.DependencyInjection).Assembly);
    opts.Discovery.IncludeAssembly(typeof(JenkinsCiDbContext).Assembly);

    // IDeploymentReleaseClient is a typed HttpClient (an opaque lambda factory registration),
    // which Wolverine's generated handler code can't construct inline — it would otherwise throw
    // InvalidServiceLocationException for the ContainerPublished/BuildSucceeded handlers (which
    // reach it via AutoPublishHandler → PromoteToReleaseHandler). Tell Wolverine to resolve it
    // from the container at runtime instead.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Jenkins.Application.Abstractions.IDeploymentReleaseClient>();

    // Same reason, for the notification fan-out: NotificationDispatcher and both senders are
    // `internal` to Cicd.Notifications, which generated handler code cannot `new` up.
    //
    // This one is load-bearing beyond its own feature. Wolverine composes EVERY handler for a
    // message type into ONE chain, and BuildSucceeded already drives AutoPublishHandler →
    // handoffs → releases → deployments. If a notifier failed codegen it would take the whole
    // chain with it and auto-publish would stop SILENTLY — no exception at the call site, nothing
    // dead-lettered. That is exactly how the DORA digest broke (9a9bed1).
    opts.CodeGeneration.AlwaysUseServiceLocationFor<INotificationDispatcher>();

    // Enrol handlers in the DbContext transaction + durable SQL Server outbox/inbox
    // (mirrors the deployment service) so integration events publish reliably.
    opts.UseEntityFrameworkCoreTransactions();
    var connection = builder.Configuration.GetConnectionString("JenkinsCi");
    if (!string.IsNullOrEmpty(connection))
    {
        opts.PersistMessagesWithSqlServer(connection);
    }

    // Cross-service event bus (provider-pluggable; RabbitMQ by default). CI publishes
    // container-published facts; it subscribes to deployment outcomes.
    opts.AddCicdMessaging(builder.Configuration, topology => topology
        .Publish<Cicd.IntegrationEvents.Ci.ContainerPublished>("ci.events")
        .Publish<Cicd.IntegrationEvents.Ci.AspireAppPublished>("ci.events")
        .Publish<Cicd.IntegrationEvents.Ci.PipelineStepCompleted>("ci.events")
        .Publish<Cicd.IntegrationEvents.Ci.PipelineCompleted>("ci.events")
        .Publish<Cicd.IntegrationEvents.Ci.PipelineFailed>("ci.events")
        .Publish<Cicd.IntegrationEvents.Ci.PipelineCancelled>("ci.events")
        .Subscribe("deployment.events", subscriber: "jenkins"));
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Apply EF migrations at startup when Database:AutoMigrate is set (compose/dev
// convenience). SQLite is local, so no retry needed.
if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<JenkinsCiDbContext>().Database.MigrateAsync();
}

// Seed the default "CICD Main" pipeline if none exist (idempotent). Runs once the
// host has started — SaveChanges dispatches domain events, which needs the Wolverine
// bus running. Non-fatal if the DB isn't migrated.
app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    try
    {
        await scope.ServiceProvider.GetRequiredService<SeedDefaultPipelineHandler>().HandleAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Default pipeline seed skipped (is the database migrated?)");
    }

    // Seed the demo tracked repositories, but ONLY on a brand-new system (no repositories at all).
    // Without this a freshly brought-up stack has an empty repository dropdown and there is nothing
    // to start a build against, even though the pipelines above were seeded. Registering a
    // repository raises only a config event with no consumer, so this never triggers a build.
    //
    // Guarded two ways: it is skipped once ANY repository exists (so it can never re-add something
    // an operator deleted, and never touches an established system), and it can be turned off with
    // Ci:SeedDemoRepositories=false for a deployment that should start empty.
    if (app.Configuration.GetValue("Ci:SeedDemoRepositories", true))
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<JenkinsCiDbContext>();
            if (!await db.Repositories.AnyAsync())
            {
                var result = await scope.ServiceProvider.GetRequiredService<Jenkins.Api.Endpoints.SeedCiHandler>()
                    .HandleAsync(new SeedDemoCiRequest(
                        AspireRepo: true,
                        CloudRunRepo: true,
                        DeployableUnitId: null,
                        DeployableUnitName: null,
                        ContainerName: null), CancellationToken.None);

                app.Logger.LogInformation(
                    "Seeded demo CI repositories on first run: {Created} created, {Skipped} skipped.",
                    result.Created, result.Skipped);
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Demo repository seed skipped.");
        }
    }
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/", () => Results.Ok(new
{
    name = "Jenkins.Api",
    status = "ready",
}));

app.MapRepositoryEndpoints();
app.MapBuildEndpoints();
app.MapHandoffEndpoints();
app.MapPipelineEndpoints();
app.MapPipelineRunEndpoints();
app.MapWebhookEndpoints();
app.MapCiResetEndpoints();
app.MapCiSeedEndpoints();

// Live pipeline-run stream (server-to-server SignalR from web-admin; no CORS needed).
app.MapHub<PipelineRunHub>("/hubs/pipeline-runs");

app.Run();
