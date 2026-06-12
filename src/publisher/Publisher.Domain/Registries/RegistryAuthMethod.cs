namespace Publisher.Domain.Registries;

/// <summary>
/// How the publisher authenticates to a remote registry. The credential itself is never stored
/// in the DB — only a reference (see <see cref="RemoteRegistry.CredentialSecretRef"/>) resolved
/// at push time, or no secret at all for <see cref="Adc"/>.
/// </summary>
public enum RegistryAuthMethod
{
    /// <summary>Application Default Credentials / Workload Identity (GAR). No stored secret.</summary>
    Adc = 0,

    /// <summary>A GCP service-account JSON key, resolved from a secret reference.</summary>
    ServiceAccountKey = 1,

    /// <summary>Username + a password/token resolved from a secret reference.</summary>
    UsernamePassword = 2,

    /// <summary>A bearer/registry token resolved from a secret reference.</summary>
    Token = 3,
}
