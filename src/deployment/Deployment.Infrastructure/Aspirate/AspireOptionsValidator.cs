using Microsoft.Extensions.Options;

namespace Deployment.Infrastructure.Aspirate;

/// <summary>
/// Fails startup (via <c>ValidateOnStart</c>) when the <c>aspirate</c> CLI can't be resolved or the
/// timeouts are non-positive — instead of letting the first Aspire deploy fail at the shell-out.
/// Mirrors <c>GoogleCloudRunOptionsValidator</c>.
/// </summary>
internal sealed class AspireOptionsValidator : IValidateOptions<AspireOptions>
{
    public ValidateOptionsResult Validate(string? name, AspireOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Executable))
            failures.Add("Deployment:Aspirate:Executable must be set (a path to aspirate, or a bare name on PATH).");
        else if (!IsResolvable(options.Executable))
            failures.Add(
                $"aspirate executable '{options.Executable}' was not found. Install Aspir8 ('dotnet tool install -g Aspirate') " +
                "and either put it on PATH or set Deployment:Aspirate:Executable (under Aspire: user-secret Parameters:AspirateExecutable) to its full path.");

        if (options.GenerateTimeoutSeconds <= 0) failures.Add("Deployment:Aspirate:GenerateTimeoutSeconds must be greater than 0.");
        if (options.ApplyTimeoutSeconds <= 0) failures.Add("Deployment:Aspirate:ApplyTimeoutSeconds must be greater than 0.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsResolvable(string exe)
    {
        if (Path.IsPathRooted(exe) || exe.Contains(Path.DirectorySeparatorChar) || exe.Contains(Path.AltDirectorySeparatorChar))
            return File.Exists(exe);

        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pathext = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { string.Empty };

        foreach (var dir in dirs)
        {
            if (File.Exists(Path.Combine(dir, exe))) return true;
            foreach (var ext in pathext)
                if (File.Exists(Path.Combine(dir, exe + ext))) return true;
        }
        return false;
    }
}
