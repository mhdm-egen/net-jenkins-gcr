namespace Publisher.Domain.Containers;

/// <summary>How a container record entered the publisher inventory.</summary>
public enum ContainerSource
{
    /// <summary>Materialized from the CI <c>ContainerPublished</c> bus event.</summary>
    Bus = 0,

    /// <summary>Added by hand from the UI (picked from the local Nexus docker registry).</summary>
    Manual = 1,
}
