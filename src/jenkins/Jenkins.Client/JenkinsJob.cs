namespace Jenkins.Client;

/// <summary>
/// Shallow listing entry for a Jenkins job (project/pipeline). <see cref="Color"/>
/// is Jenkins's classic status indicator (e.g., "blue", "red", "yellow", "disabled",
/// "blue_anime" for in-progress); it doubles as the only state info available
/// without a per-job round trip.
/// </summary>
public sealed record JenkinsJobSummary(
    string Name,
    string Url,
    string? Color,
    bool Buildable,
    int? LastBuildNumber);

/// <summary>
/// Per-job details fetched on demand. <see cref="Parameters"/> is empty for
/// non-parameterized jobs.
/// </summary>
public sealed record JenkinsJobDetails(
    string Name,
    string? Description,
    IReadOnlyList<JenkinsParameterDefinition> Parameters);

public enum JenkinsParameterType
{
    Unknown,
    String,
    Text,
    Boolean,
    Choice,
    Password
}

/// <summary>
/// One parameter declaration on a parameterized job. <see cref="Choices"/> is
/// populated only for <see cref="JenkinsParameterType.Choice"/>.
/// </summary>
public sealed record JenkinsParameterDefinition(
    string Name,
    JenkinsParameterType Type,
    string? Description,
    string? DefaultValue,
    IReadOnlyList<string> Choices);

/// <summary>
/// One archived artifact attached to a Jenkins build.
/// <see cref="RelativePath"/> is the path under the build's <c>artifact/</c> URL.
/// </summary>
public sealed record JenkinsBuildArtifact(
    string FileName,
    string RelativePath);

/// <summary>
/// Rich view of a single Jenkins build. Carries everything <see cref="Build"/> does,
/// plus the artifacts list and the human-readable causes that triggered the run.
/// Returned from <c>GetBuildDetailsAsync</c> — heavier than the listing call, so
/// fetched on demand (e.g., when the user clicks into a build summary page).
/// </summary>
public sealed record JenkinsBuildDetails(
    int Number,
    string Url,
    bool Building,
    BuildResult? Result,
    long Timestamp,
    long Duration,
    string? Description,
    IReadOnlyList<JenkinsBuildArtifact> Artifacts,
    // shortDescription strings extracted from actions[].causes[].shortDescription.
    // Typical values: "Started by user admin", "Started by upstream project X build #42",
    // "Started by timer". Multi-element when more than one cause applies.
    IReadOnlyList<string> Causes);
