namespace Publisher.Domain.Registries;

/// <summary>Remote container-registry flavors the publisher can push to.</summary>
public enum RegistryProvider
{
    GoogleArtifactRegistry = 0,
    DockerHub = 1,
    AzureContainerRegistry = 2,
    GenericV2 = 3,
}
