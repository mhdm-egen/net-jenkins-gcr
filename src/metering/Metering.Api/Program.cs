using System.Text.Json.Serialization;
using Metering.Api.Endpoints;
using Metering.Application;
using Metering.Application.Observability;
using Metering.Infrastructure;
using Metering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Custom meter into the OTel pipeline (composes with AddServiceDefaults).
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter(MeteringTelemetry.Name));

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddMeteringApplication();
builder.Services.AddMeteringInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();

// Apply EF migrations on startup (SQL may not be ready on cold start — retry).
if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MeteringDbContext>();
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

app.MapGet("/", () => Results.Ok(new { name = "Metering.Api", status = "ready" }));
app.MapUsageEndpoints();

app.Run();
