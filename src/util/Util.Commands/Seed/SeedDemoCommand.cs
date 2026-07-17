using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Deployment.Contracts.Seed;
using Jenkins.Contracts.Seed;
using Util.Commands.Abstractions;

namespace Util.Commands.Seed;

/// <summary>
/// Installs curated demo <b>configuration</b> (the inverse of the data reset) by POSTing to the
/// deployment + CI seed endpoints, so an operator can then trigger real builds/deploys. Additive and
/// idempotent — re-running skips anything already present. Deployment seed runs first; its Cloud Run
/// service coordinates are threaded into the CI seed for the container→component mapping.
/// </summary>
public sealed class SeedDemoCommand : ICommand
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public string Name => "seed-demo";

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        string? deploymentUrl = null, jenkinsApiUrl = null;
        bool aspire = false, bluegreen = false, cloudrun = false, k8sAdmin = false, anyScenario = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--deployment-url" when i + 1 < args.Length: deploymentUrl = args[++i]; break;
                case "--jenkins-api-url" when i + 1 < args.Length: jenkinsApiUrl = args[++i]; break;
                case "--aspire": aspire = anyScenario = true; break;
                case "--bluegreen": bluegreen = anyScenario = true; break;
                case "--cloudrun": cloudrun = anyScenario = true; break;
                case "--k8s-admin": k8sAdmin = anyScenario = true; break;
                case "--help" or "-h": PrintUsage(); return 0;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        // No scenario flags = seed everything.
        if (!anyScenario) { aspire = bluegreen = cloudrun = k8sAdmin = true; }

        var depUrl = deploymentUrl ?? Environment.GetEnvironmentVariable("DEPLOYMENT_URL") ?? "http://localhost:7230";
        var ciUrl  = jenkinsApiUrl ?? Environment.GetEnvironmentVariable("JENKINS_API_URL") ?? "http://localhost:7229";

        using var http = new HttpClient();
        var failed = false;

        // --- Deployment config first (returns the Cloud Run service coords for the CI component mapping) ---
        Guid? cloudRunServiceId = null;
        string? cloudRunServiceName = null, cloudRunContainer = null;

        Console.WriteLine($"Seeding deployment config → {depUrl}");
        try
        {
            var result = await PostAsync<SeedDemoRequest, SeedDemoResultDto>(
                http, $"{depUrl.TrimEnd('/')}/api/deployment/seed-demo",
                new SeedDemoRequest(aspire, bluegreen, cloudrun, k8sAdmin), cancellationToken);
            foreach (var line in result.Log) Console.WriteLine($"  {line}");
            Console.WriteLine($"  deployment: {result.Created} created, {result.Skipped} skipped");
            cloudRunServiceId = result.CloudRunServiceId;
            cloudRunServiceName = result.CloudRunServiceName;
            cloudRunContainer = result.CloudRunContainerName;
        }
        catch (Exception ex) { Console.Error.WriteLine($"  ERROR — deployment seed: {ex.Message}"); failed = true; }

        // --- CI config (demo repos + container→component mapping) ---
        if (aspire || cloudrun)
        {
            Console.WriteLine($"Seeding CI config → {ciUrl}");
            try
            {
                var result = await PostAsync<SeedDemoCiRequest, SeedCiResultDto>(
                    http, $"{ciUrl.TrimEnd('/')}/api/jenkins/ci/seed-demo",
                    new SeedDemoCiRequest(aspire, cloudrun, cloudRunServiceId, cloudRunServiceName, cloudRunContainer), cancellationToken);
                foreach (var line in result.Log) Console.WriteLine($"  {line}");
                Console.WriteLine($"  ci: {result.Created} created, {result.Skipped} skipped");
            }
            catch (Exception ex) { Console.Error.WriteLine($"  ERROR — CI seed: {ex.Message}"); failed = true; }
        }

        Console.WriteLine(failed ? "Demo seed completed with errors." : "Demo seed complete.");
        return failed ? 1 : 0;
    }

    private static async Task<TResp> PostAsync<TReq, TResp>(HttpClient http, string url, TReq body, CancellationToken ct)
    {
        using var resp = await http.PostAsJsonAsync(url, body, Json, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TResp>(Json, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Empty body from {url}");
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage: Util.Cli seed-demo [scenarios] [options]

            Installs curated demo configuration (repos, services, environments, mappings, Aspire apps)
            so you can trigger real builds/deploys. Additive + idempotent (re-run skips existing items);
            creates no runs.

            Scenarios (omit all to seed everything):
              --aspire       Aspire → auto-deploy (aspire-sample repo + Aspire app 'sampleapp')
              --bluegreen    Blue-green / canary K8s services + mappings
              --cloudrun     Container/NuGet Cloud Run repo + service + mapping
              --k8s-admin    Ensure the sampleapp-dev environment (k8s admin reads the live cluster)

            Options:
              --deployment-url <url>    Deployment API base (default: $DEPLOYMENT_URL or http://localhost:7230)
              --jenkins-api-url <url>   Jenkins API base   (default: $JENKINS_API_URL or http://localhost:7229)
              --help, -h                Show this help

            Environment variables: DEPLOYMENT_URL, JENKINS_API_URL
            """);
    }
}
