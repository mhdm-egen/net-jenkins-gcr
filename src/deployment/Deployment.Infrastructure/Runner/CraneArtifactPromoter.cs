using System.Diagnostics;
using Deployment.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deployment.Infrastructure.Runner;

/// <summary>
/// <see cref="IArtifactPromoter"/> backed by <c>crane copy</c> — a digest-preserving
/// registry-to-registry copy (the standard tool for this). The executable is
/// configured via <see cref="GoogleCloudRunOptions.CraneExecutable"/> and must be
/// authenticated to both the source (Nexus) and destination (GAR) registries
/// (typically via the ambient docker config / gcloud credential helper).
///
/// <c>crane copy</c> is idempotent: copying a digest that already exists at the
/// destination is effectively a no-op.
/// </summary>
internal sealed class CraneArtifactPromoter : IArtifactPromoter
{
    private readonly IOptionsMonitor<GoogleCloudRunOptions> _options;
    private readonly ILogger<CraneArtifactPromoter> _logger;

    public CraneArtifactPromoter(
        IOptionsMonitor<GoogleCloudRunOptions> options,
        ILogger<CraneArtifactPromoter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task EnsureCopiedAsync(string sourceRef, string destinationRef, CancellationToken cancellationToken = default)
    {
        var exe = _options.CurrentValue.CraneExecutable;
        if (string.IsNullOrWhiteSpace(exe)) exe = "crane";

        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("copy");
        psi.ArgumentList.Add(sourceRef);
        psi.ArgumentList.Add(destinationRef);

        _logger.LogInformation("[promote] {Exe} copy {Source} {Dest}", exe, sourceRef, destinationRef);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start '{exe}' for image copy (is it installed and on PATH?): {ex.Message}", ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{exe} copy' exited {process.ExitCode}. {stderr.Trim()} {stdout.Trim()}".Trim());
        }
    }
}
