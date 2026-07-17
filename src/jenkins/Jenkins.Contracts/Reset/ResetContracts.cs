namespace Jenkins.Contracts.Reset;

/// <summary>Selection for a CI-history reset. <see cref="BuildHistory"/> wipes the build mirror
/// (builds + artifacts + publications + handoffs); <see cref="PipelineRuns"/> wipes server-side pipeline
/// runs + console. <see cref="PruneJenkinsServer"/> deletes the builds on the real Jenkins jobs so the
/// mirror wipe sticks (otherwise the sync re-ingests them). Pipeline/job definitions are never touched.</summary>
public sealed record ResetCiRequest(bool BuildHistory, bool PipelineRuns, bool PruneJenkinsServer);

/// <summary>Counts from a CI-history reset: mirror build rows + pipeline-run rows deleted, and (if pruning)
/// how many builds were deleted on the Jenkins server across how many jobs.</summary>
public sealed record CiResetResultDto(int Builds, int PipelineRuns, int JenkinsBuildsDeleted, int JobsPruned);
