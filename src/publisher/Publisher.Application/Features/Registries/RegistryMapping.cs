using Publisher.Contracts.Registries;
using Publisher.Domain.Registries;

namespace Publisher.Application.Features.Registries;

internal static class RegistryMapping
{
    public static RemoteRegistryDto ToDto(this RemoteRegistry r) => new(
        Id: r.Id,
        Name: r.Name,
        Provider: (RegistryProviderDto)(int)r.Provider,
        RegistryHost: r.RegistryHost,
        RepositoryPath: r.RepositoryPath,
        AuthMethod: (RegistryAuthMethodDto)(int)r.AuthMethod,
        Username: r.Username,
        CredentialSecretRef: r.CredentialSecretRef,
        IsDefault: r.IsDefault,
        Enabled: r.Enabled,
        CreatedAtUtc: r.CreatedAtUtc,
        UpdatedAtUtc: r.UpdatedAtUtc);

    public static RegistryProvider ToDomain(this RegistryProviderDto p) => (RegistryProvider)(int)p;
    public static RegistryAuthMethod ToDomain(this RegistryAuthMethodDto a) => (RegistryAuthMethod)(int)a;
}
