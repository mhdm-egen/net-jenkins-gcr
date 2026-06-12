using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Publisher.Application.Abstractions;
using Publisher.Domain.Registries;

namespace Publisher.Infrastructure.Pushing;

/// <summary>
/// <see cref="IRegistryPusher"/> backed by <c>crane</c> — a digest-preserving, daemonless,
/// idempotent registry-to-registry copy (mirrors the deployment service's promoter). For
/// credentialed auth methods it runs <c>crane auth login</c> first (password via stdin); for
/// <see cref="RegistryAuthMethod.Adc"/> it relies on ambient credentials (Workload Identity /
/// gcloud credential helper). The source (Nexus) must likewise be reachable via the ambient
/// docker config.
///
/// <c>crane copy</c> on a digest that already exists at the destination is effectively a no-op.
/// </summary>
public sealed class CraneRegistryPusher : IRegistryPusher
{
    private readonly IOptionsMonitor<PublisherPushOptions> _options;
    private readonly ILogger<CraneRegistryPusher> _logger;

    public CraneRegistryPusher(IOptionsMonitor<PublisherPushOptions> options, ILogger<CraneRegistryPusher> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task PushAsync(string sourceRef, string destinationRef, RegistryCredential credential, CancellationToken cancellationToken = default)
    {
        var exe = string.IsNullOrWhiteSpace(_options.CurrentValue.CraneExecutable)
            ? "crane"
            : _options.CurrentValue.CraneExecutable;

        await AuthenticateIfNeededAsync(exe, destinationRef, credential, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[push] {Exe} copy {Source} {Dest}", exe, sourceRef, destinationRef);
        await RunAsync(exe, new[] { "copy", sourceRef, destinationRef }, stdin: null, cancellationToken).ConfigureAwait(false);
    }

    private async Task AuthenticateIfNeededAsync(string exe, string destinationRef, RegistryCredential credential, CancellationToken ct)
    {
        if (credential.Method == RegistryAuthMethod.Adc) return; // ambient credentials

        var host = HostOf(destinationRef);
        var (user, password) = credential.Method switch
        {
            RegistryAuthMethod.ServiceAccountKey => ("_json_key", credential.Secret),
            RegistryAuthMethod.UsernamePassword => (credential.Username ?? string.Empty, credential.Secret),
            RegistryAuthMethod.Token => (credential.Username ?? "oauth2accesstoken", credential.Secret),
            _ => (credential.Username ?? string.Empty, credential.Secret),
        };

        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException($"No credential resolved for registry host '{host}' ({credential.Method}).");

        _logger.LogInformation("[push] {Exe} auth login {Host} (user {User})", exe, host, user);
        await RunAsync(exe, new[] { "auth", "login", host, "-u", user, "--password-stdin" }, stdin: password, ct).ConfigureAwait(false);
    }

    private static string HostOf(string reference)
    {
        var slash = reference.IndexOf('/');
        return slash > 0 ? reference[..slash] : reference;
    }

    private async Task RunAsync(string exe, string[] args, string? stdin, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start '{exe}' (is it installed and on PATH?): {ex.Message}", ex);
        }

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"'{exe} {string.Join(' ', args)}' exited {process.ExitCode}. {stderr.Trim()} {stdout.Trim()}".Trim());
    }
}
