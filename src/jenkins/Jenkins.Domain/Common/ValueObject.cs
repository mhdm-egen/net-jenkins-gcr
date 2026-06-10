namespace Jenkins.Domain.Common;

/// <summary>
/// Base for objects identified by the combination of their property values rather
/// than by an Id. Subclasses define equality components via <see cref="GetEqualityComponents"/>.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>Yield the fields that participate in equality, in stable order.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    public bool Equals(ValueObject? other) =>
        other is not null
        && GetType() == other.GetType()
        && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override int GetHashCode() =>
        GetEqualityComponents()
            .Aggregate(0, (hash, c) => HashCode.Combine(hash, c?.GetHashCode() ?? 0));

    public static bool operator ==(ValueObject? a, ValueObject? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(ValueObject? a, ValueObject? b) => !(a == b);
}
