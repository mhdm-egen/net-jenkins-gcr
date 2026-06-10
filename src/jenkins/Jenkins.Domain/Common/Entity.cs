namespace Jenkins.Domain.Common;

/// <summary>
/// Base for any persistence-identity-bearing object. Equality is by Id, not by
/// reference or value — two Entity instances with the same Id are the same
/// entity even if their state has drifted.
/// </summary>
/// <typeparam name="TId">Identity type — typically <see cref="Guid"/>.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public bool Equals(Entity<TId>? other) =>
        other is not null && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);

    public static bool operator ==(Entity<TId>? a, Entity<TId>? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(Entity<TId>? a, Entity<TId>? b) => !(a == b);
}
