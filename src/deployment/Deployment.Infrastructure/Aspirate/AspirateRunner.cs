using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Deployment.Application.Abstractions;
using Deployment.Infrastructure.Nexus;

namespace Deployment.Infrastructure.Aspirate;

/// <summary>
/// <see cref="IAspirateRunner"/> over the Aspir8 CLI. Fetches the CI-produced Kustomize output archive
/// (the <c>aspirate generate</c> result), extracts it, repoints the image registry host + digest-pins
/// from Nexus, then runs <c>aspirate apply -i &lt;dir&gt; -k &lt;context&gt;</c>. Captures combined
/// stdout+stderr as the run log (ANSI-stripped). No source / no <c>generate</c> on the deploy host.
/// </summary>
internal sealed partial class AspirateRunner : IAspirateRunner
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    private readonly IOptionsMonitor<AspireOptions> _options;
    private readonly IOptionsMonitor<NexusRegistryOptions> _nexus;
    private readonly INexusImageDigestResolver _digests;
    private readonly ILogger<AspirateRunner> _logger;

    public AspirateRunner(
        IOptionsMonitor<AspireOptions> options, IOptionsMonitor<NexusRegistryOptions> nexus,
        INexusImageDigestResolver digests, ILogger<AspirateRunner> logger)
    {
        _options = options;
        _nexus = nexus;
        _digests = digests;
        _logger = logger;
    }

    public async Task<AspirateDeployResult> DeployAsync(AspirateDeployRequest request, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var exe = string.IsNullOrWhiteSpace(opts.Executable) ? "aspirate" : opts.Executable;
        var log = new StringBuilder();

        var root = string.IsNullOrWhiteSpace(opts.WorkingRoot) ? Path.GetTempPath() : opts.WorkingRoot;
        var workDir = Path.Combine(root, "aspire-deploy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            // 1) Acquire the kustomize output into the work dir.
            string outputDir;
            try
            {
                outputDir = await AcquireOutputAsync(request.ManifestSource, workDir, log, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.AppendLine(ex.Message);
                return new AspirateDeployResult(false, log.ToString(), $"could not fetch manifest source: {ex.Message}");
            }

            // 2) Repoint registry host + digest-pin the images.
            if (!string.IsNullOrWhiteSpace(opts.PullRegistry))
                await RewriteImageHostAsync(outputDir, opts.PullRegistry.Trim(), log, cancellationToken).ConfigureAwait(false);

            // 3) Apply to the target context.
            var env = string.IsNullOrWhiteSpace(opts.Kubeconfig) ? null
                : new Dictionary<string, string> { ["KUBECONFIG"] = opts.Kubeconfig };
            var apply = await RunAsync(exe, new[]
            {
                "apply",
                "-i", outputDir,
                "-k", request.KubeContext,
                "--non-interactive",
                "--disable-secrets",
                "--disable-state",
            }, workDir, env, opts.ApplyTimeoutSeconds, log, cancellationToken).ConfigureAwait(false);

            return apply == 0
                ? new AspirateDeployResult(true, log.ToString(), null)
                : new AspirateDeployResult(false, log.ToString(), $"aspirate apply exited {apply}: {Summarize(log)}");
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>Downloads (URL) or copies (local archive/dir) the kustomize output into the work dir; returns the dir holding the root kustomization.yaml.</summary>
    private async Task<string> AcquireOutputAsync(string source, string workDir, StringBuilder log, CancellationToken ct)
    {
        var dest = Path.Combine(workDir, "output");

        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            log.AppendLine($"fetching manifest archive: {source}");
            using var req = new HttpRequestMessage(HttpMethod.Get, source);
            var n = _nexus.CurrentValue;
            if (!string.IsNullOrEmpty(n.Username))
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($"{n.Username}:{n.Password}")));
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            ExtractArchive(bytes, dest);
        }
        else if (Directory.Exists(source))
        {
            CopyDirectory(source, dest);
        }
        else if (File.Exists(source))
        {
            ExtractArchive(await File.ReadAllBytesAsync(source, ct).ConfigureAwait(false), dest);
        }
        else
        {
            throw new InvalidOperationException($"manifest source '{source}' is not a URL, directory, or archive file.");
        }

        return FindKustomizationRoot(dest)
            ?? throw new InvalidOperationException("no kustomization.yaml found in the manifest output.");
    }

    private static void ExtractArchive(byte[] bytes, string dest)
    {
        Directory.CreateDirectory(dest);
        // Sniff magic bytes: PK = zip; 1F 8B = gzip (tar.gz).
        if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
        {
            using var ms = new MemoryStream(bytes);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gz, dest, overwriteFiles: true);
        }
        else
        {
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            zip.ExtractToDirectory(dest, overwriteFiles: true);
        }
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(src, dir)));
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(dest, Path.GetRelativePath(src, file)), overwrite: true);
    }

    /// <summary>The shallowest directory containing a kustomization.yaml — the apply input.</summary>
    private static string? FindKustomizationRoot(string dir)
        => Directory.EnumerateFiles(dir, "kustomization.yaml", SearchOption.AllDirectories)
            .OrderBy(p => p.Count(c => c == Path.DirectorySeparatorChar))
            .Select(Path.GetDirectoryName)
            .FirstOrDefault();

    private async Task<int> RunAsync(string exe, string[] args, string workingDir, IReadOnlyDictionary<string, string>? env, int timeoutSeconds, StringBuilder log, CancellationToken ct)
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
        if (env is not null) foreach (var (k, v) in env) psi.Environment[k] = v;

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
    /// Appends a Kustomize <c>images:</c> override to the root kustomization that repoints every image's
    /// registry host to <paramref name="pullRegistry"/>, and — when the Nexus digest resolver is configured —
    /// pins each to its immutable <c>@sha256</c> digest. No-op if the kustomization already declares overrides.
    /// </summary>
    private async Task RewriteImageHostAsync(string outputPath, string pullRegistry, StringBuilder log, CancellationToken ct)
    {
        var kustomization = Path.Combine(outputPath, "kustomization.yaml");
        if (!File.Exists(kustomization)) return;
        var existing = File.ReadAllText(kustomization);
        if (existing.Contains("\nimages:", StringComparison.Ordinal)) return; // don't double-apply

        var found = new Dictionary<string, (string NewName, string Repo, string Tag)>(StringComparer.Ordinal);
        foreach (var dep in Directory.GetFiles(outputPath, "deployment.yaml", SearchOption.AllDirectories))
        {
            foreach (Match m in ImageLine().Matches(File.ReadAllText(dep)))
            {
                var refStr = m.Groups[1].Value.Trim();
                var slash = refStr.IndexOf('/');
                if (slash <= 0) continue;
                var host = refStr[..slash];
                if (!host.Contains(':') && !host.Contains('.')) continue;          // not a registry host
                var rest = refStr[(slash + 1)..];
                if (rest.Contains('@')) continue;                                  // already digest-pinned
                var colon = rest.IndexOf(':');
                var repo = colon >= 0 ? rest[..colon] : rest;
                var tag = colon >= 0 ? rest[(colon + 1)..] : "latest";
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
