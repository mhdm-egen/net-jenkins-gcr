namespace Jenkins.Client;

public sealed record Build(
    int Number,
    string Url,
    bool Building,
    BuildResult? Result,
    long Duration,
    long Timestamp,
    // Optional — Jenkinsfile sets this via `currentBuild.description`. For cicd-build
    // it carries "<PACKAGE_VERSION> (<gitShort>)", so listings can show the version
    // without a per-row build-info.json fetch. Default = null keeps existing callers
    // and JSON shapes backward-compatible.
    string? Description = null);
