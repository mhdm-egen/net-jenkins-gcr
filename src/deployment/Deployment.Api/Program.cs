using System.Text.Json.Serialization;
using Deployment.Api.Endpoints;
using Deployment.Application;
using Deployment.Infrastructure;
using Deployment.Infrastructure.Persistence;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Match the Blazor client's JSON shape: enums on the wire are strings
// (ServiceKindDto = "WebApi", not 0). Without this, Minimal API parameter
// binding fails with "Failed to read parameter '... body'" the moment a
// payload contains an enum value.
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Application layer: FluentValidation registrations + any pure use-case services.
builder.Services.AddDeploymentApplication();

// Infrastructure layer: EF Core DbContext + repositories + UnitOfWork.
// Connection string lives at ConnectionStrings:Deployment.
builder.Services.AddDeploymentInfrastructure(builder.Configuration);

// In-process deployment runner. Disable via Deployment:Runner:Enabled=false
// when running a separate worker host that owns the runner end-to-end.
builder.Services.AddDeploymentRunner(builder.Configuration);

// Wolverine: CQRS dispatcher + in-process bus. Two notable conveniences:
//   .UseEntityFrameworkCoreTransactions() makes handlers automatically participate
//      in the DeploymentDbContext's transaction (commits with SaveChangesAsync).
//   .PersistMessagesWithSqlServer(...) sets up the outbox + inbox tables on the
//      same DB so messages survive crashes and never get lost between bus and DB.
// Handlers are discovered by convention from Application + Infrastructure assemblies.
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Deployment.Application.DependencyInjection).Assembly);
    opts.Discovery.IncludeAssembly(typeof(DeploymentDbContext).Assembly);

    opts.UseEntityFrameworkCoreTransactions();

    var connection = builder.Configuration.GetConnectionString("Deployment");
    if (!string.IsNullOrEmpty(connection))
    {
        opts.PersistMessagesWithSqlServer(connection);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Ok(new
{
    name = "Deployment.Api",
    status = "ready",
}));

app.MapCatalogServiceEndpoints();
app.MapCatalogApplicationEndpoints();
app.MapReleaseEndpoints();
app.MapEnvironmentEndpoints();
app.MapConfigurationEndpoints();
app.MapDeploymentEndpoints();
app.MapRunnerEndpoints();

app.Run();
