namespace Jenkins.Domain.PipelineRuns;

/// <summary>
/// Immutable record of one completed step within a <see cref="PipelineRun"/>: the Jenkins
/// job, its resulting build number, and the build result. Persisted as part of the run
/// (JSON column) since steps are always read together with the run.
/// </summary>
public sealed record PipelineRunStepRecord(int Order, string JobName, int BuildNumber, string Result);
