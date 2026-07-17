using System.Text.Json.Serialization;
using Cicd.Messaging;
using Cicd.Notifications;
using Deployment.Api.Endpoints;
using Deployment.Api.Hubs;
using Deployment.Application;
using Deployment.Application.Abstractions;
using Deployment.Infrastructure;
using Deployment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Wire the custom deploy Meter + ActivitySource into the OTel pipeline (composes with ServiceDefaults).
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter(Deployment.Application.Observability.DeploymentTelemetry.Name))
    .WithTracing(t => t.AddSource(Deployment.Application.Observability.DeploymentTelemetry.Name));

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(opts => opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDeploymentApplication();
builder.Services.AddDeploymentInfrastructure(builder.Configuration);

// SignalR: broadcasts deployment-run completion to the web-admin for app-wide toasts.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IDeploymentRunNotifier, DeploymentRunNotifier>();

// Admin "danger zone" data reset (bulk history/data deletes; keeps config).
builder.Services.AddScoped<Deployment.Api.Endpoints.ResetDeploymentHandler>();

// Deploy notifications (Slack / email) — opt-in via Deployment:Notifications. The in-process
// notification handlers fan out through INotificationDispatcher on the deploy domain events.
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Deployment:Notifications"));
builder.Services.AddCicdNotifications();

// Wolverine: CQRS dispatcher + in-process bus + durable cross-service messaging.
// Handlers (the ContainerPublished consumer, the run executor, the success translator) are
// discovered from Application + Infrastructure.
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Deployment.Application.DependencyInjection).Assembly);
    opts.Discovery.IncludeAssembly(typeof(DeploymentDbContext).Assembly);

    // The repositories, step executors, and the directly-invoked RequestDeploymentHandler all resolve
    // to *internal* concrete types (Infrastructure persistence/steps). Wolverine's generated handler
    // code can't `new` up an internal type, so without this it throws InvalidServiceLocationException
    // (ServiceLocationPolicy.NotAllowed) and the DeploymentRunRequested/ContainerPublished handlers
    // never compile — leaving deployment runs stuck Pending. Tell Wolverine to resolve these from the
    // container at runtime instead (mirrors the jenkins service's IDeploymentReleaseClient handling).
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Domain.Runs.IDeploymentRunRepository>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Domain.Mappings.IDeploymentMappingRepository>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Domain.Services.IServiceRepository>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Domain.Containers.IKnownContainerRepository>();
    // Service-located (not inlined) on purpose: the registry's constructor takes the executor
    // collection, and letting Wolverine inline `new StepExecutorRegistry(IEnumerable<…>)` would
    // re-trigger the codegen bug it exists to avoid (a mis-materialised executor collection).
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Application.Abstractions.IStepExecutorRegistry>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Application.Features.Runs.RequestDeploymentHandler>();
    // The completion notifier wraps IHubContext, which the generated handler can't construct inline.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<IDeploymentRunNotifier>();
    // Aspire-app deploy: the run executor resolves these internal/Infrastructure types — same codegen
    // constraint as above, or the AspireApplicationRunRequested handler leaves runs stuck Pending.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Domain.AspireApps.Runs.IAspireApplicationRunRepository>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<IAspirateRunner>();
    // Resolve the telemetry singleton from the container (it has a public ctor Wolverine would otherwise
    // new up, creating a duplicate, undisposed Meter).
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Application.Observability.DeploymentTelemetry>();
    // The executor also snapshots deployed images via this reader (internal Infrastructure impl) — service-locate it.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Application.Features.AspireApps.IAspireClusterStatusReader>();
    // The Aspire executor deletes namespaces for blue-green promote/rollback via this (internal impl).
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Application.Abstractions.INamespaceManager>();
    // The preview executor stamps a browsable Ingress via this (internal impl).
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Application.Abstractions.IIngressManager>();
    // Preview-environment executor resolves the internal preview repository — service-locate it (same constraint).
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Domain.Previews.IPreviewEnvironmentRepository>();
    // AspireAppPublished consumer (CI→deploy handoff): the repository is an internal Infrastructure type
    // and the directly-invoked deploy handler transitively news up more of them — service-locate both so
    // the handler compiles (same constraint as the ContainerPublished consumer above).
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Domain.AspireApps.IAspireApplicationRepository>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Application.Features.AspireApps.RequestAspireDeploymentHandler>();
    // The consumer also routes non-main-branch publishes into preview environments via this handler.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<Deployment.Application.Features.Previews.CreatePreviewEnvironmentHandler>();
    // Deploy-notification handlers inject the dispatcher (with internal senders behind it) — service-locate it.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<INotificationDispatcher>();

    // Blue-green Aspire deploys block their handler on a health-gate poll (up to ~2 min) before settling.
    // Wolverine's default 60s execution timeout would cancel the handler's token mid-gate, leaving the run
    // stuck Pending with the green namespace leaked. Give handlers ample headroom (deploys are otherwise
    // seconds); the gate self-bounds its own wall-clock.
    opts.DefaultExecutionTimeout = TimeSpan.FromMinutes(10);

    opts.UseEntityFrameworkCoreTransactions();

    var connection = builder.Configuration.GetConnectionString("Deployment");
    if (!string.IsNullOrEmpty(connection))
        opts.PersistMessagesWithSqlServer(connection);

    // Consume CI container-published facts; publish service-deployed / -failed facts.
    opts.AddCicdMessaging(builder.Configuration, topology => topology
        .Publish<Cicd.IntegrationEvents.Deployment.ServiceDeployed>("deployment.events")
        .Publish<Cicd.IntegrationEvents.Deployment.ServiceDeploymentFailed>("deployment.events")
        .Publish<Cicd.IntegrationEvents.Deployment.AspireApplicationDeployed>("deployment.events")
        .Publish<Cicd.IntegrationEvents.Deployment.AspireApplicationDeploymentFailed>("deployment.events")
        .Subscribe("ci.events", subscriber: "deployment"));
});

var app = builder.Build();
app.MapDefaultEndpoints();

if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DeploymentDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch (Exception ex) when (attempt < 12)
        {
            app.Logger.LogWarning(ex, "DB migrate attempt {Attempt} failed; retrying in 5s…", attempt);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.MapGet("/", () => Results.Ok(new { name = "Deployment.Api", status = "ready" }));

app.MapServiceEndpoints();
app.MapEnvironmentEndpoints();
app.MapMappingEndpoints();
app.MapRunEndpoints();
app.MapAspireAppEndpoints();
app.MapAspireRunEndpoints();
app.MapPreviewEndpoints();
app.MapK8sEndpoints();
app.MapResetEndpoints();
app.MapHub<DeploymentRunHub>("/hubs/deployment-runs");

app.Run();
