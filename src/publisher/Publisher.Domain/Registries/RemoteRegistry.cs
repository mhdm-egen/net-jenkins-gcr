using Publisher.Domain.Common;
using Publisher.Domain.Registries.Events;

namespace Publisher.Domain.Registries;

/// <summary>
/// A configured remote container registry the publisher can push images to (the managed
/// "push target" list). Holds only non-secret connection config plus a <see cref="CredentialSecretRef"/>
/// — a NAME the runtime resolves to an actual credential at push time. No raw secret is ever
/// stored here. The default target is typically a Google Artifact Registry using
/// <see cref="RegistryAuthMethod.Adc"/> (no secret at all).
/// </summary>
public sealed class RemoteRegistry : AggregateRoot<Guid>
{
    /// <summary>Unique, human-friendly identifier, e.g. <c>gar-prod</c>.</summary>
    public string Name { get; private set; }

    public RegistryProvider Provider { get; private set; }

    /// <summary>Registry host, e.g. <c>us-docker.pkg.dev</c> or <c>registry-1.docker.io</c>.</summary>
    public string RegistryHost { get; private set; }

    /// <summary>Path/namespace under the host, e.g. <c>my-project/my-repo</c> (GAR) or an org name.</summary>
    public string RepositoryPath { get; private set; }

    public RegistryAuthMethod AuthMethod { get; private set; }

    /// <summary>Username for <see cref="RegistryAuthMethod.UsernamePassword"/>; otherwise null.</summary>
    public string? Username { get; private set; }

    /// <summary>
    /// Name of the secret holding the credential (e.g. an env-var key like
    /// <c>Publisher__Registries__gar-prod__Key</c>). Null for <see cref="RegistryAuthMethod.Adc"/>.
    /// </summary>
    public string? CredentialSecretRef { get; private set; }

    public bool IsDefault { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private RemoteRegistry()
    {
        Name = string.Empty;
        RegistryHost = string.Empty;
        RepositoryPath = string.Empty;
    }

    public RemoteRegistry(
        Guid id,
        string name,
        RegistryProvider provider,
        string registryHost,
        string repositoryPath,
        RegistryAuthMethod authMethod,
        string? username,
        string? credentialSecretRef,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(registryHost)) throw new ArgumentException("RegistryHost cannot be empty.", nameof(registryHost));

        Id = id;
        Name = name.Trim();
        Provider = provider;
        RegistryHost = registryHost.Trim();
        RepositoryPath = repositoryPath?.Trim() ?? string.Empty;
        AuthMethod = authMethod;
        Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        CredentialSecretRef = string.IsNullOrWhiteSpace(credentialSecretRef) ? null : credentialSecretRef.Trim();
        Enabled = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;

        ValidateAuth();
        RaiseEvent(new RemoteRegistryRegistered(Id, Name, Provider, RegistryHost, RepositoryPath, createdAtUtc));
    }

    public void UpdateConfiguration(
        RegistryProvider provider,
        string registryHost,
        string repositoryPath,
        RegistryAuthMethod authMethod,
        string? username,
        string? credentialSecretRef,
        DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(registryHost)) throw new ArgumentException("RegistryHost cannot be empty.", nameof(registryHost));

        Provider = provider;
        RegistryHost = registryHost.Trim();
        RepositoryPath = repositoryPath?.Trim() ?? string.Empty;
        AuthMethod = authMethod;
        Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        CredentialSecretRef = string.IsNullOrWhiteSpace(credentialSecretRef) ? null : credentialSecretRef.Trim();
        UpdatedAtUtc = occurredAtUtc;

        ValidateAuth();
        RaiseEvent(new RemoteRegistryUpdated(Id, Name, Provider, RegistryHost, RepositoryPath, occurredAtUtc));
    }

    public void ChangeActivation(bool enabled, DateTimeOffset occurredAtUtc)
    {
        if (Enabled == enabled) return;
        Enabled = enabled;
        UpdatedAtUtc = occurredAtUtc;
        RaiseEvent(new RemoteRegistryActivationChanged(Id, enabled, occurredAtUtc));
    }

    /// <summary>Sets/clears the default flag. Single-default invariant is enforced at the application layer.</summary>
    public void SetDefault(bool isDefault, DateTimeOffset occurredAtUtc)
    {
        if (IsDefault == isDefault) return;
        IsDefault = isDefault;
        UpdatedAtUtc = occurredAtUtc;
        RaiseEvent(new RemoteRegistryDefaultChanged(Id, isDefault, occurredAtUtc));
    }

    private void ValidateAuth()
    {
        switch (AuthMethod)
        {
            case RegistryAuthMethod.Adc:
                // No secret expected.
                break;
            case RegistryAuthMethod.UsernamePassword:
                if (string.IsNullOrWhiteSpace(Username))
                    throw new ArgumentException("Username is required for UsernamePassword auth.");
                if (CredentialSecretRef is null)
                    throw new ArgumentException("CredentialSecretRef is required for UsernamePassword auth.");
                break;
            case RegistryAuthMethod.ServiceAccountKey:
            case RegistryAuthMethod.Token:
                if (CredentialSecretRef is null)
                    throw new ArgumentException($"CredentialSecretRef is required for {AuthMethod} auth.");
                break;
        }
    }
}
