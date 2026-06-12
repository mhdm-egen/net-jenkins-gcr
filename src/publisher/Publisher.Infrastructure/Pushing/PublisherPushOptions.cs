namespace Publisher.Infrastructure.Pushing;

/// <summary>Options for the image-push subsystem. Bound from the <c>Publisher:Push</c> section.</summary>
public sealed class PublisherPushOptions
{
    public const string SectionName = "Publisher:Push";

    /// <summary>Path to the <c>crane</c> executable. Defaults to <c>crane</c> (resolved on PATH).</summary>
    public string CraneExecutable { get; set; } = "crane";
}
