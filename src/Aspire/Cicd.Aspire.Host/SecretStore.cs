using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cicd.Aspire.Host;

/// <summary>
/// Generate-once, reuse-forever secrets backed by this AppHost's user-secrets file.
///
/// WHY NOT Aspire's own generator: `AddParameter(name, new GenerateParameterDefault{...},
/// secret: true, persist: true)` produces a NEW value on every run in Aspire 13.4.3 — verified by
/// reading secrets.json across consecutive starts (sql-password went jnqY4JVQ... -> jm6yFDFe...,
/// nexus-password EG3wgBY2... -> GUfZE3kq... -> k8v3EY94...). That is fatal for any credential a
/// container bakes into its data volume on first init and never updates:
///   * SQL Server  -> "Login failed for user 'sa'. Reason: Password did not match" on the 2nd run.
///   * Nexus       -> the admin password stops matching /nexus-data, so provisioning cannot log in.
/// Both were observed before this helper existed.
///
/// So we own the lifecycle: read the key, and only if absent generate a value and write it back,
/// preserving everything else in the file. The AppHost then passes the resulting fixed string to
/// AddParameter — the same "eager value" shape the Nexus parameters already use, which also avoids
/// the interactive prompt that blocks a headless `dotnet run`.
/// </summary>
internal static class SecretStore
{
    // Deliberately excludes '$' (collides with JCasC ${} interpolation in
    // jenkins/controller/casc/jenkins.yaml) and ':' (separator in HTTP basic auth, which both the
    // Jenkins and Nexus clients use). Everything else is alphanumeric for maximum tool compatibility.
    private const string Alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// Returns the stored value for <paramref name="key"/> under "Parameters:", generating and
    /// persisting a strong random one on first call. Falls back to an in-memory value (with a
    /// warning) if the file cannot be written, so a read-only environment still boots.
    /// </summary>
    public static string GetOrCreate(string userSecretsId, string key, int length = 24)
    {
        var path = SecretsPath(userSecretsId);
        var fullKey = $"Parameters:{key}";

        JsonObject root;
        try
        {
            root = File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            root = new JsonObject();
        }

        if (root[fullKey]?.GetValue<string>() is { Length: > 0 } existing)
            return existing;

        var generated = Generate(length);
        root[fullKey] = generated;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"[apphost] generated and stored '{fullKey}' in user-secrets.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[apphost] WARNING: could not persist '{fullKey}' ({ex.Message}). Using an in-memory " +
                $"value for this run only — containers that bake credentials into a data volume " +
                $"(sql, nexus) will fail to authenticate on the next start.");
        }

        return generated;
    }

    private static string Generate(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    /// <summary>The same location `dotnet user-secrets` uses, so the CLI can read and edit these.</summary>
    private static string SecretsPath(string userSecretsId)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return OperatingSystem.IsWindows()
            ? Path.Combine(appData, "Microsoft", "UserSecrets", userSecretsId, "secrets.json")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".microsoft", "usersecrets", userSecretsId, "secrets.json");
    }
}
