namespace Metering.Domain;

/// <summary>
/// What is being metered. The ledger is general-purpose; <see cref="AiTokens"/> is the
/// first meter. The rest are placeholders for the Phase-2 build/deploy/storage meters
/// (fed from ci.events / deployment.events and scheduled gauge collectors).
/// </summary>
public enum MeterKind
{
    AiTokens = 0,
    BuildCompute = 1,
    DeployRun = 2,
    NexusStorage = 3,
    DockerStorage = 4,
    CloudRunCompute = 5,
    K8sResource = 6,
}

/// <summary>
/// Counter = accumulating flow (tokens, build seconds); Gauge = point-in-time level
/// (storage bytes, running pods). Recorded per-sample so summaries can treat them right.
/// </summary>
public enum MeterType
{
    Counter = 0,
    Gauge = 1,
}
