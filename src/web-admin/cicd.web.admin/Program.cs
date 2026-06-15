using Cicd.Web.Admin.Components;
using Cicd.Web.Admin.Services.Gcp;
using Cicd.Web.Admin.Services.Nexus;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Nexus — Url + repository name come from configuration; Password MUST come from
// env var (Nexus__Password) per project security policy. Missing creds don't fail
// startup; the Nuget/Docker pages surface the configuration error.
var nexusOptions = builder.Configuration.GetSection("Nexus").Get<NexusOptions>() ?? new NexusOptions();
builder.Services.AddSingleton(nexusOptions);
builder.Services.AddSingleton<INexusClient, NexusClient>();

// Google Cloud — projects list comes from configuration; the GcpClient resolves
// Application Default Credentials at construction. If creds are missing, the
// client records the error but doesn't fail startup (the Google page surfaces it).
var gcpOptions = builder.Configuration.GetSection("Google").Get<GcpOptions>() ?? new GcpOptions();
builder.Services.AddSingleton(gcpOptions);
builder.Services.AddSingleton<IGcpClient, GcpClient>();

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
