using Deployment.Domain.Common;

namespace Deployment.Domain.Previews.Events;

/// <summary>A preview environment was requested — drives the executor that shells out to aspirate to apply
/// the app's manifest into the preview namespace.</summary>
public sealed record PreviewEnvironmentRequested(Guid PreviewId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
