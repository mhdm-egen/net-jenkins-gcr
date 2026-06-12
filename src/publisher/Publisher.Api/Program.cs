using System.Text.Json.Serialization;
using Cicd.Messaging;
using Microsoft.EntityFrameworkCore;
using Publisher.Api.Endpoints;
using Publisher.Application;
using Publisher.Infrastructure;
using Publisher.Infrastructure.Persistence;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

// Enums on the wire are strings, matching the other services' clients.
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Application layer: FluentValidation registrations + use-case handlers.
builder.Services.AddPublisherApplication();

// Infrastructure layer: EF Core DbContext + repositories + UnitOfWork + readers.
// Connection string lives at ConnectionStrings:Publisher.
builder.Services.AddPublisherInfrastructure(builder.Configuration);

// Wolverine: CQRS dispatcher + in-process bus + durable cross-service messaging.
//   .UseEntityFrameworkCoreTransactions() enrolls handlers in the DbContext transaction.
//   .PersistMessagesWithSqlServer(...) provisions the outbox/inbox on the same DB.
// Handlers (incl. the ContainerPublished bus consumer) are discovered from Application + Infrastructure.
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Publisher.Application.DependencyInjection).Assembly);
    opts.Discovery.IncludeAssembly(typeof(PublisherDbContext).Assembly);

    opts.UseEntityFrameworkCoreTransactions();

    var connection = builder.Configuration.GetConnectionString("Publisher");
    if (!string.IsNullOrEmpty(connection))
    {
        opts.PersistMessagesWithSqlServer(connection);
    }

    // Cross-service event bus (provider-pluggable; RabbitMQ by default). The publisher consumes CI
    // container-published facts and publishes its own container-promoted facts on publisher.events.
    opts.AddCicdMessaging(builder.Configuration, topology => topology
        .Publish<Cicd.IntegrationEvents.Publisher.ContainerPromoted>("publisher.events")
        .Subscribe("ci.events", subscriber: "publisher"));
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Apply EF migrations at startup when Database:AutoMigrate is set (compose/dev convenience).
// Retries so a not-yet-ready SQL Server doesn't crash the boot.
if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PublisherDbContext>();
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
    name = "Publisher.Api",
    status = "ready",
}));

app.MapContainersEndpoints();
app.MapChannelsEndpoints();
app.MapRegistriesEndpoints();
app.MapRulesEndpoints();
app.MapPromotionsEndpoints();

app.Run();
