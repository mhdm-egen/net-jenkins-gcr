using System.Diagnostics;
using System.Text.Json.Nodes;
using Util.Commands.Abstractions;

namespace Util.Commands.Nexus;

/// <summary>
/// Brings an empty Nexus up to the state this platform assumes: the four repositories, the docker
/// connector on :8082, and the realms that make `docker login` and `dotnet nuget push` work.
///
/// Run as a one-shot by the Aspire AppHost (WaitForCompletion) on every start, so every step is
/// idempotent — the /nexus-data volume persists and this must be a no-op on the second run.
///
/// Replaces what was previously manual: the Nexus setup wizard, hand-clicking four repositories in
/// the UI, and the curl in docs/sbom-setup.md. nexus/wire-cleanup-policies.sh remains the optional
/// retention layer on top — it needs these repositories to exist first.
/// </summary>
public sealed class ProvisionCommand : ICommand
{
    public string Name => "nexus-provision";

    /// <summary>The password sonatype/nexus3 sets when NEXUS_SECURITY_RANDOMPASSWORD=false.</summary>
    private const string DeterministicFirstRunPassword = "admin123";

    private const string RealmDockerToken = "DockerToken";
    private const string RealmNuGetApiKey = "NuGetApiKey";

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        string? urlOverride = null, userOverride = null, passOverride = null, dockerRepoOverride = null;
        var readyTimeout = TimeSpan.FromMinutes(5);

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url" when i + 1 < args.Length: urlOverride = args[++i]; break;
                case "--user" when i + 1 < args.Length: userOverride = args[++i]; break;
                case "--password" when i + 1 < args.Length: passOverride = args[++i]; break;
                case "--docker-repo" when i + 1 < args.Length: dockerRepoOverride = args[++i]; break;
                case "--timeout" when i + 1 < args.Length && int.TryParse(args[i + 1], out var secs):
                    readyTimeout = TimeSpan.FromSeconds(secs); i++; break;
                case "--help" or "-h": PrintUsage(); return 0;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        var url = urlOverride ?? Environment.GetEnvironmentVariable("NEXUS_URL") ?? "http://localhost:8081";
        var user = userOverride ?? Environment.GetEnvironmentVariable("NEXUS_USER") ?? "admin";
        var pass = passOverride ?? Environment.GetEnvironmentVariable("NEXUS_PASS");
        var dockerRepo = dockerRepoOverride
                         ?? Environment.GetEnvironmentVariable("NEXUS_DOCKER_REPO")
                         ?? "docker-private";

        if (string.IsNullOrEmpty(pass))
        {
            Console.Error.WriteLine("ERROR: NEXUS_PASS env var (or --password flag) is required.");
            return 2;
        }

        Console.WriteLine($"nexus-provision: target {url} (docker repo '{dockerRepo}')");

        // ---- 1. readiness ---------------------------------------------------------------------
        if (!await WaitUntilUpAsync(url, readyTimeout, cancellationToken))
        {
            Console.Error.WriteLine($"ERROR: Nexus at {url} did not become ready within {readyTimeout.TotalSeconds:0}s.");
            return 1;
        }

        // ---- 2. authenticate, bootstrapping the admin password if this is a first run -----------
        var client = await AuthenticateAsync(url, user, pass, cancellationToken);
        if (client is null)
        {
            Console.Error.WriteLine(
                $"ERROR: could not authenticate to Nexus as '{user}'.\n" +
                $"  Tried: the configured password, the first-run default, and /nexus-data/admin.password.\n" +
                $"  If this volume was provisioned with a different password, either set it as\n" +
                $"  Parameters:nexus-password in the AppHost user-secrets, or reset the volume:\n" +
                $"    docker rm -f nexus && docker volume rm nexus-data");
            return 1;
        }

        using (client)
        {
            // ---- 3. EULA (Community Edition builds only) ----------------------------------------
            var eula = await client.IsEulaAcceptedAsync(cancellationToken);
            if (eula is false)
            {
                await client.AcceptEulaAsync(cancellationToken);
                Console.WriteLine("  EULA accepted.");
            }

            // ---- 4. repositories ----------------------------------------------------------------
            var existing = await client.GetRepositoryNamesAsync(cancellationToken);

            await EnsureRepoAsync(client, existing, "nuget", "nuget-hosted", NuGetHosted("nuget-hosted"), cancellationToken);
            await EnsureRepoAsync(client, existing, "raw", "sboms", RawHosted("sboms"), cancellationToken);
            await EnsureRepoAsync(client, existing, "raw", "raw-hosted", RawHosted("raw-hosted"), cancellationToken);
            await EnsureDockerRepoAsync(client, existing, dockerRepo, cancellationToken);

            // ---- 5. realms ----------------------------------------------------------------------
            // DockerToken is what makes `docker login nexus:8082` work. NuGetApiKey is kept active
            // for anyone publishing with a real API key, even though the default path is basic auth.
            var realms = await client.GetActiveRealmsAsync(cancellationToken);
            var missing = new[] { RealmDockerToken, RealmNuGetApiKey }
                .Where(r => !realms.Contains(r, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            if (missing.Length > 0)
            {
                await client.SetActiveRealmsAsync(realms.Concat(missing), cancellationToken);
                Console.WriteLine($"  realms activated: {string.Join(", ", missing)}");
            }
            else
            {
                Console.WriteLine("  realms already active: DockerToken, NuGetApiKey");
            }

            // NOTE there is deliberately no step that pushes a NuGet API key into Jenkins.
            // Nexus 3.70+ removed the internal endpoint that exposed per-user NuGet API keys (it now
            // 404s), and Nexus rejects the account password in the --api-key slot. Publishing
            // therefore uses basic auth, and since the Nexus password is already known when JCasC
            // runs, the 'nexus-nuget' credential is created up front by
            // jenkins/controller/casc/jenkins.yaml — no post-startup injection needed.
        }

        // ---- 6. advisory: the build-agent image ------------------------------------------------
        WarnIfAgentImageMissing();

        Console.WriteLine("nexus-provision: done.");
        return 0;
    }

    // ---------------------------------------------------------------------------------------------

    private static async Task<bool> WaitUntilUpAsync(string url, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        var announced = false;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await NexusAdminClient.IsUpAsync(url, ct)) return true;

            if (!announced)
            {
                Console.WriteLine("  waiting for Nexus to come up (cold start takes ~2 min) ...");
                announced = true;
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (TaskCanceledException) { return false; }
        }

        return false;
    }

    /// <summary>
    /// The bootstrap ladder. Returns an authenticated client, rotating the admin password to the
    /// configured one when this is a first run.
    /// </summary>
    private static async Task<NexusAdminClient?> AuthenticateAsync(
        string url, string user, string pass, CancellationToken ct)
    {
        // (a) already provisioned
        var configured = new NexusAdminClient(new NexusOptions(url, user, pass));
        if (await configured.CanAuthenticateAsync(ct))
        {
            Console.WriteLine("  authenticated with the configured password.");
            return configured;
        }
        configured.Dispose();

        // (b) the deterministic first-run password, then (c) the random one on the volume
        foreach (var (candidate, source) in await BootstrapCandidatesAsync(ct))
        {
            using var boot = new NexusAdminClient(new NexusOptions(url, user, candidate));
            if (!await boot.CanAuthenticateAsync(ct)) continue;

            Console.WriteLine($"  first run detected ({source}); rotating the admin password.");
            await boot.ChangeAdminPasswordAsync(user, pass, ct);

            var rotated = new NexusAdminClient(new NexusOptions(url, user, pass));
            if (await rotated.CanAuthenticateAsync(ct)) return rotated;
            rotated.Dispose();
        }

        return null;
    }

    private static async Task<List<(string Password, string Source)>> BootstrapCandidatesAsync(CancellationToken ct)
    {
        var candidates = new List<(string, string)>
        {
            (DeterministicFirstRunPassword, "NEXUS_SECURITY_RANDOMPASSWORD=false")
        };

        var onVolume = await TryReadAdminPasswordFileAsync(ct);
        if (!string.IsNullOrWhiteSpace(onVolume))
            candidates.Add((onVolume.Trim(), "/nexus-data/admin.password"));

        return candidates;
    }

    /// <summary>Reads the random first-run password Nexus writes into its data volume.</summary>
    private static async Task<string?> TryReadAdminPasswordFileAsync(CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var a in new[] { "exec", "nexus", "cat", "/nexus-data/admin.password" })
                psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return null;

            var stdout = await p.StandardOutput.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            return p.ExitCode == 0 ? stdout : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task EnsureRepoAsync(
        NexusAdminClient client, HashSet<string> existing,
        string format, string name, JsonObject body, CancellationToken ct)
    {
        if (existing.Contains(name))
        {
            Console.WriteLine($"  repository '{name}' ({format}) already exists.");
            return;
        }

        await client.CreateHostedRepositoryAsync(format, body, ct);
        Console.WriteLine($"  repository '{name}' ({format}) created.");
    }

    /// <summary>
    /// The docker repo additionally owns the :8082 connector — that port IS the registry endpoint
    /// every Jenkinsfile pushes to, so if the repo exists but the port drifted, correct it.
    /// </summary>
    private static async Task EnsureDockerRepoAsync(
        NexusAdminClient client, HashSet<string> existing, string name, CancellationToken ct)
    {
        if (!existing.Contains(name))
        {
            await client.CreateHostedRepositoryAsync("docker", DockerHosted(name), ct);
            Console.WriteLine($"  repository '{name}' (docker) created with the HTTP connector on 8082.");
            return;
        }

        var port = await client.GetDockerHttpPortAsync(name, ct);
        if (port == 8082)
        {
            Console.WriteLine($"  repository '{name}' (docker) already exists with the connector on 8082.");
            return;
        }

        await client.UpdateHostedRepositoryAsync("docker", name, DockerHosted(name), ct);
        Console.WriteLine($"  repository '{name}' (docker) connector corrected {port?.ToString() ?? "none"} -> 8082.");
    }


    private static void WarnIfAgentImageMissing()
    {
        try
        {
            var psi = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var a in new[] { "image", "inspect", "netsdk10:latest" }) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return;
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit(TimeSpan.FromSeconds(20));

            if (p.ExitCode != 0)
            {
                Console.Error.WriteLine(
                    "  WARNING: the build-agent image 'netsdk10:latest' is missing. Every cicd-* job runs its " +
                    "stages in that image and will fail without it. Start the 'build-agent-image' resource in " +
                    "the Aspire dashboard, or run devops/build-build-container.ps1.");
            }
        }
        catch
        {
            // advisory only — never fail provisioning over this
        }
    }

    // ---- repository payloads --------------------------------------------------------------------

    private static JsonObject Storage(string writePolicy) => new()
    {
        ["blobStoreName"] = "default",
        ["strictContentTypeValidation"] = true,
        ["writePolicy"] = writePolicy
    };

    private static JsonObject NuGetHosted(string name) => new()
    {
        ["name"] = name,
        ["online"] = true,
        ["storage"] = Storage("ALLOW")
    };

    // sboms and raw-hosted are both redeploy-friendly: a rebuilt commit re-uploads the same path.
    private static JsonObject RawHosted(string name) => new()
    {
        ["name"] = name,
        ["online"] = true,
        ["storage"] = Storage("ALLOW"),
        ["raw"] = new JsonObject { ["contentDisposition"] = "ATTACHMENT" }
    };

    private static JsonObject DockerHosted(string name) => new()
    {
        ["name"] = name,
        ["online"] = true,
        ["storage"] = Storage("ALLOW_ONCE"),
        ["docker"] = new JsonObject
        {
            ["v1Enabled"] = false,
            // false => the DockerToken realm issues bearer tokens, which is what `docker login` uses.
            ["forceBasicAuth"] = false,
            ["httpPort"] = 8082
        }
    };

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage: Util.Cli nexus-provision [options]

            Creates the repositories, realms and docker connector this platform expects. Idempotent.

              --url <url>            Nexus base URL         (env NEXUS_URL,         default http://localhost:8081)
              --user <user>          Admin user             (env NEXUS_USER,        default admin)
              --password <pw>        Admin password         (env NEXUS_PASS,        required)
              --docker-repo <name>   Docker hosted repo     (env NEXUS_DOCKER_REPO, default docker-private)
              --timeout <seconds>    Readiness wait         (default 300)

            Optional, for pushing the NuGet API key into Jenkins:
              env JENKINS_URL, JENKINS_USER (default admin), JENKINS_PASS
            """);
    }
}
