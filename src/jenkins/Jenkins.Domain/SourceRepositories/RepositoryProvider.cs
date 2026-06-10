namespace Jenkins.Domain.SourceRepositories;

/// <summary>Source-control host a tracked repository lives on.</summary>
public enum RepositoryProvider
{
    GitHub = 0,
    AzureDevOps = 1,
    GitLab = 2,
    Bitbucket = 3,
    Other = 4,
}
