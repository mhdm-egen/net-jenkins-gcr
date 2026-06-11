namespace Deployment.Domain.Releases;

/// <summary>
/// A single BOM entry inside an Application <see cref="Release"/>:
/// "this app release contains service S, with pin mode P (and possibly version V)."
///
/// Composite identity: (ApplicationReleaseId, ServiceId). Child entity of the
/// Application Release aggregate; mutated only via methods on <see cref="Release"/>.
///
/// Pin-mode invariant (CHECK constraint at DB layer, validated here too):
///   (PinMode = Pinned AND ServiceReleaseId IS NOT NULL)
///   OR (PinMode IN (Latest, Current) AND ServiceReleaseId IS NULL)
/// </summary>
public sealed class ReleaseComposition
{
    public Guid ApplicationReleaseId { get; private set; }
    public Guid ServiceId { get; private set; }
    public PinMode PinMode { get; private set; }

    /// <summary>
    /// Bound only when <see cref="PinMode"/> is <c>Pinned</c>. For Latest/Current
    /// the resolver fills it in at deploy time; the persisted row stays null.
    /// </summary>
    public Guid? ServiceReleaseId { get; private set; }

    private ReleaseComposition() { }

    internal ReleaseComposition(
        Guid applicationReleaseId,
        Guid serviceId,
        PinMode pinMode,
        Guid? serviceReleaseId)
    {
        if (applicationReleaseId == Guid.Empty)
            throw new ArgumentException("ApplicationReleaseId cannot be empty.", nameof(applicationReleaseId));
        if (serviceId == Guid.Empty)
            throw new ArgumentException("ServiceId cannot be empty.", nameof(serviceId));

        ValidatePinInvariant(pinMode, serviceReleaseId);

        ApplicationReleaseId = applicationReleaseId;
        ServiceId = serviceId;
        PinMode = pinMode;
        ServiceReleaseId = serviceReleaseId;
    }

    internal void Update(PinMode pinMode, Guid? serviceReleaseId)
    {
        ValidatePinInvariant(pinMode, serviceReleaseId);
        PinMode = pinMode;
        ServiceReleaseId = serviceReleaseId;
    }

    private static void ValidatePinInvariant(PinMode pinMode, Guid? serviceReleaseId)
    {
        if (pinMode == PinMode.Pinned)
        {
            if (serviceReleaseId is null || serviceReleaseId == Guid.Empty)
                throw new InvalidOperationException(
                    "PinMode=Pinned requires a non-empty ServiceReleaseId.");
        }
        else
        {
            if (serviceReleaseId is not null)
                throw new InvalidOperationException(
                    $"PinMode={pinMode} requires ServiceReleaseId to be null.");
        }
    }
}
