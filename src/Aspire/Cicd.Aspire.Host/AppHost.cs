using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Nexus — the admin UI browses the docker/nuget registries (Docker / NuGet pages). Read the
// parameter values from the AppHost's user-secrets explicitly (builder.Configuration does not
// surface them at AddParameter time) and pass them eagerly so the resource never blocks on a
// headless interactive prompt. Override via user-secrets; defaults are the docker-network names.
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

builder.AddProject<Projects.cicd_web_admin>("web-admin")
    .WithEnvironment("Nexus__Url", nexusUrl)
    .WithEnvironment("Nexus__Password", nexusPassword)
    .WithEnvironment("Nexus__DockerRegistryHost", nexusDockerHost)
    .WithEnvironment("Nexus__DockerHostedRepository", nexusDockerRepo);

builder.Build().Run();
