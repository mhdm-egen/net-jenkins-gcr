using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Deployment.Application.Abstractions;
using Deployment.Infrastructure.Nexus;

namespace Deployment.Infrastructure.Aspirate;

/// <summary>
/// <see cref="IAspirateRunner"/> over the Aspir8 CLI: <c>aspirate generate --skip-build</c> (Kustomize
/// manifests from the already-pushed Nexus images) then <c>aspirate apply -k &lt;context&gt;</c>. Process
/// shell-out modelled on <c>CraneArtifactPromoter</c> — captures combined stdout+stderr as the run log.
/// </summary>
internal sealed partial class AspirateRunner : IAspirateRunner
{
    private readonly IOptionsMonitor<AspireOptions> _options;
    private readonly INexusImageDigestResolver _digests;
    private readonly ILogger<AspirateRunner> _logger;

    public AspirateRunner(IOptionsMonitor<AspireOptions> options, INexusImageDigestResolver digests, ILogger<AspirateRunner> logger)
    {
        _options = options;
        _digests = digests;
        _logger = logger;
    }

    public async Task<AspirateDeployResult> DeployAsync(AspirateDeployRequest request, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var exe = string.IsNullOrWhiteSpace(opts.Executable) ? "aspirate" : opts.Executable;
        var log = new StringBuilder();

        if (!Directory.Exists(request.AppHostPath))
            return new AspirateDeployResult(false, log.ToString(), $"AppHostPath '{request.AppHostPath}' does not exist.");
        if (!File.Exists(Path.Combine(request.AppHostPath, "aspirate.json")))
            return new AspirateDeployResult(false, log.ToString(), $"No aspirate.json in '{request.AppHostPath}' — run 'aspirate init' there first.");

        var outputPath = Path.Combine(request.AppHostPath, "aspirate-output");

        // 1) generate --skip-build → Kustomize manifests referencing the already-pushed Nexus images.
        // Run with the AppHost as the working directory and the default project-path (".") — passing an
        // explicit -p makes aspirate resolve the generated manifest.json against the parent dir.
        var generate = await RunAsync(exe, new[]
        {
            "generate",
            "-o", outputPath,
            "--skip-build",
            "--namespace", request.Namespace,
            "--image-pull-policy", opts.ImagePullPolicy,
            "--output-format", "kustomize",
            "--include-dashboard", "false",
            "--non-interactive",
            "--disable-secrets",
        }, request.AppHostPath, opts.GenerateTimeoutSeconds, log, cancellationToken).ConfigureAwait(false);

        if (generate != 0)
            return new AspirateDeployResult(false, log.ToString(), $"aspirate generate exited {generate}: {Summarize(log)}");

        // 1b) Rewrite the build registry host to a node-reachable pull registry, if configured. aspirate
        // bakes the build/push registry (e.g. localhost:8082) into the manifests, which a cluster node
        // can't resolve; a Kustomize images: override repoints them (e.g. host.docker.internal:8082).
        if (!string.IsNullOrWhiteSpace(opts.PullRegistry))
            await RewriteImageHostAsync(outputPath, opts.PullRegistry.Trim(), log, cancellationToken).ConfigureAwait(false);

        // 2) apply → deploy the manifests to the target kube context.
        var apply = await RunAsync(exe, new[]
        {
            "apply",
            "-i", outputPath,
            "-k", request.KubeContext,
            "--non-interactive",
            "--disable-secrets",
        }, request.AppHostPath, opts.ApplyTimeoutSeconds, log, cancellationToken).ConfigureAwait(false);

        if (apply != 0)
            return new AspirateDeployResult(false, log.ToString(), $"aspirate apply exited {apply}: {Summarize(log)}");

        return new AspirateDeployResult(true, log.ToString(), null);
    }

    private async Task<int> RunAsync(string exe, string[] args, string workingDir, int timeoutSeconds, StringBuilder log, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        log.AppendLine($"$ {exe} {string.Join(' ', args)}");
        _logger.LogInformation("[aspire] {Exe} {Args}", exe, string.Join(' ', args));

        using var process = new Process { StartInfo = psi };
        try { process.Start(); }
        catch (Exception ex)
        {
            log.AppendLine($"could not start '{exe}': {ex.Message}");
            throw new InvalidOperationException($"Could not start '{exe}' (installed and on PATH?): {ex.Message}", ex);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            log.AppendLine($"(timed out after {timeoutSeconds}s)");
            return -1;
        }

        var stdout = Ansi().Replace(await stdoutTask.ConfigureAwait(false), string.Empty);
        var stderr = Ansi().Replace(await stderrTask.ConfigureAwait(false), string.Empty);
        if (stdout.Length > 0) log.AppendLine(stdout.TrimEnd());
        if (stderr.Length > 0) log.AppendLine(stderr.TrimEnd());
        return process.ExitCode;
    }

    /// <summary>
    /// Appends a Kustomize <c>images:</c> override to the generated root kustomization that repoints every
    /// image's registry host to <paramref name="pullRegistry"/>, and — when the Nexus digest resolver is
    /// configured — pins each to its immutable <c>@sha256</c> digest (preserving build provenance even
    /// though aspirate emits a floating tag). No-op if the kustomization already declares overrides.
    /// </summary>
    private async Task RewriteImageHostAsync(string outputPath, string pullRegistry, StringBuilder log, CancellationToken ct)
    {
        var kustomization = Path.Combine(outputPath, "kustomization.yaml");
        if (!File.Exists(kustomization)) return;
        var existing = File.ReadAllText(kustomization);
        if (existing.Contains("\nimages:", StringComparison.Ordinal)) return; // don't double-apply

        // name (host/repo) -> (newName, repo, tag)
        var found = new Dictionary<string, (string NewName, string Repo, string Tag)>(StringComparer.Ordinal);
        foreach (var dep in Directory.GetFiles(outputPath, "deployment.yaml", SearchOption.AllDirectories))
        {
            foreach (Match m in ImageLine().Matches(File.ReadAllText(dep)))
            {
                var refStr = m.Groups[1].Value.Trim();
                var slash = refStr.IndexOf('/');
                if (slash <= 0) continue;
                var host = refStr[..slash];
                if (!host.Contains(':') && !host.Contains('.')) continue;          // not a registry host (e.g. library/x)

                var rest = refStr[(slash + 1)..];
                if (rest.Contains('@')) continue;                                  // already digest-pinned — leave it
                var colon = rest.IndexOf(':');
                var repo = colon >= 0 ? rest[..colon] : rest;
                var tag = colon >= 0 ? rest[(colon + 1)..] : "latest";
                // Repoint the host (and, below, digest-pin) — also pins when host is already the pull registry.
                found[$"{host}/{repo}"] = ($"{pullRegistry}/{repo}", repo, tag);
            }
        }
        if (found.Count == 0) return;

        var sb = new StringBuilder(existing.TrimEnd()).AppendLine().AppendLine().AppendLine("images:");
        int pinned = 0;
        foreach (var (name, info) in found)
        {
            sb.AppendLine($"- name: {name}").AppendLine($"  newName: {info.NewName}");
            var digest = await _digests.ResolveDigestAsync(info.Repo, info.Tag, ct).ConfigureAwait(false);
            if (digest is { Length: > 0 }) { sb.AppendLine($"  digest: {digest}"); pinned++; }
            else sb.AppendLine($"  newTag: {info.Tag}");
        }
        File.WriteAllText(kustomization, sb.ToString());
        log.AppendLine($"(rewrote image registry host -> {pullRegistry} for {found.Count} image(s); {pinned} digest-pinned)");
    }

    [GeneratedRegex(@"^\s*image:\s*(\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex ImageLine();

    /// <summary>A concise one-liner for the failure reason: the first error-ish line, else the last non-empty line.</summary>
    private static string Summarize(StringBuilder log)
    {
        var lines = log.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var err = Array.FindLast(lines, l => l.Contains("error", StringComparison.OrdinalIgnoreCase) || l.Contains('!'));
        return (err ?? (lines.Length > 0 ? lines[^1] : "no output")).Trim();
    }

    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
    private static partial Regex Ansi();
}
