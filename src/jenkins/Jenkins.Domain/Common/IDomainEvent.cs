namespace Jenkins.Domain.Common;

/// <summary>
/// Marker for things-that-happened in the CI domain. Concrete events are records
/// declared in the relevant aggregate's <c>Events/</c> folder (e.g.
/// <c>BuildSucceeded</c>). Dispatched by the Infrastructure's UnitOfWork after
/// SaveChangesAsync via Wolverine's in-process bus (same pattern as the
/// deployment service).
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
