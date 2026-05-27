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
