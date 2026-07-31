using System.Diagnostics;

namespace Cicd.Aspire.Host;

/// <summary>
/// Ensures a fixed, well-known docker network exists before any resource starts, and can attach a
/// container to it after the fact.
///
/// WHY: Jenkins runs every pipeline stage in a SIBLING container that it launches itself through the
/// mounted docker socket — see the BUILD_CONTAINER_ARGS defaults in jenkins/*/Jenkinsfile and
/// jenkins/jobs/cicd-jobs.groovy, all of which hard-code "--net=cicd-net". Those containers are NOT
/// Aspire-managed and never join Aspire's own network, whose name carries a per-session random
/// postfix ("aspire-session-network-&lt;rand&gt;-Cicd.Aspire.Host") and therefore can never be baked
/// into a job parameter. So we keep a second, stable network and attach Nexus (and Jenkins) to BOTH.
/// Build agents then resolve "nexus:8081"/"nexus:8082" by container name via Docker's embedded DNS.
/// </summary>
internal static class DockerNetwork
{
    /// <summary>Creates the network if it does not already exist. Warns rather than throws.</summary>
    public static void EnsureExists(string name)
    {
        if (Run(15, out _, "network", "inspect", name) == 0)
            return;

        if (Run(30, out var err, "network", "create", "--driver", "bridge", name) == 0)
        {
            Console.WriteLine($"[apphost] created docker network '{name}'.");
            return;
        }

        Console.Error.WriteLine(
            $"[apphost] WARNING: could not create docker network '{name}' ({err.Trim()}). Jenkins build " +
            $"agents will not be able to resolve 'nexus'. Create it manually: docker network create {name}");
    }

    /// <summary>
    /// Attaches an existing container to a network. Used as the fallback path when passing a second
    /// "--network" through WithContainerRuntimeArgs does not take effect. Connecting a container that
    /// is already attached exits non-zero with "already exists" — treated as success.
    /// </summary>
    public static void Connect(string network, string container)
    {
        if (Run(30, out var err, "network", "connect", network, container) == 0)
        {
            Console.WriteLine($"[apphost] attached '{container}' to docker network '{network}'.");
            return;
        }

        if (err.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            return;

        Console.Error.WriteLine(
            $"[apphost] WARNING: could not attach '{container}' to '{network}': {err.Trim()}");
    }

    private static int Run(int timeoutSeconds, out string stderr, params string[] args)
    {
        stderr = string.Empty;
        try
        {
            var psi = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return -1;

            stderr = p.StandardError.ReadToEnd();
            _ = p.StandardOutput.ReadToEnd();

            if (!p.WaitForExit(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
                stderr = $"timed out after {timeoutSeconds}s";
                return -1;
            }

            return p.ExitCode;
        }
        catch (Exception ex)
        {
            stderr = ex.Message;
            return -1;
        }
    }
}
