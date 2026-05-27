namespace Jenkins.Orchestrator;

public static class DefaultPipelines
{
    /// <summary>
    /// The canonical cicd chain: build -> (publish-nuget || publish-nexus-docker) ->
    /// publish-gar -> publish-gcr. Run sequentially by the orchestrator;
    /// each downstream pulls SOURCE_BUILD_NUMBER from its declared upstream.
    /// </summary>
    public static IReadOnlyList<PipelineStep> CicdMain() => new[]
    {
        new PipelineStep("cicd-build"),
        new PipelineStep("cicd-publish-nexus-nuget",  UpstreamJob: "cicd-build"),
        new PipelineStep("cicd-publish-nexus-docker", UpstreamJob: "cicd-build"),
        new PipelineStep("cicd-publish-gcp-gar",      UpstreamJob: "cicd-publish-nexus-docker"),
        new PipelineStep("cicd-publish-gcp-gcr",      UpstreamJob: "cicd-publish-gcp-gar"),
    };
}
