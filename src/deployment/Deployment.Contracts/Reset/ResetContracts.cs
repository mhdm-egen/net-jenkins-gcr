namespace Deployment.Contracts.Reset;

/// <summary>Selection for a deployment-data reset. Each flag wipes one data/history target; config
/// (services, environments, mappings, Aspire app definitions) is never touched.</summary>
public sealed record ResetDeploymentRequest(bool Runs, bool AspireRuns, bool Previews, bool Containers);

/// <summary>Row counts actually deleted per target (0 for unselected).</summary>
public sealed record ResetDeploymentResultDto(int Runs, int AspireRuns, int Previews, int Containers);
