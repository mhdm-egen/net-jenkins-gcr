namespace Publisher.Contracts.Registries;

public enum RegistryProviderDto
{
    GoogleArtifactRegistry = 0,
    DockerHub = 1,
    AzureContainerRegistry = 2,
    GenericV2 = 3,
}

public enum RegistryAuthMethodDto
{
    Adc = 0,
    ServiceAccountKey = 1,
    UsernamePassword = 2,
    Token = 3,
}

/// <summary>
/// Wire shape of a configured remote registry. Note there is no credential value — only the
/// <see cref="CredentialSecretRef"/> name; secrets are resolved server-side at push time.
/// </summary>
public sealed record RemoteRegistryDto(
    Guid Id,
    string Name,
    RegistryProviderDto Provider,
    string RegistryHost,
    string RepositoryPath,
    RegistryAuthMethodDto AuthMethod,
    string? Username,
    string? CredentialSecretRef,
    bool IsDefault,
    bool Enabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateRegistryRequest(
    string Name,
    RegistryProviderDto Provider,
    string RegistryHost,
    string RepositoryPath,
    RegistryAuthMethodDto AuthMethod,
    string? Username,
    string? CredentialSecretRef,
    bool MakeDefault);

public sealed record UpdateRegistryRequest(
    RegistryProviderDto Provider,
    string RegistryHost,
    string RepositoryPath,
    RegistryAuthMethodDto AuthMethod,
    string? Username,
    string? CredentialSecretRef);
