namespace Deployment.Domain.Deployments;

/// <summary>
/// Lifecycle of a single <see cref="Deployment"/> row. State machine: see
/// decisions §6.3.
///
/// <list type="bullet">
/// <item><c>Queued → Running → Succeeded | Failed</c></item>
/// <item><c>Queued → Cancelled</c> (queue cleanup; not allowed from Running in v1)</item>
/// <item><c>Succeeded → RolledBack</c> (set when a *new* rollback deployment
///   targeting this row's earlier release succeeds; original row never otherwise mutates)</item>
/// </list>
///
/// <see cref="HealthChecking"/> is reserved for v2 async health-verification
/// and is never written by v1 code (decisions §6.2).
/// </summary>
public enum DeploymentStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    RolledBack = 4,
    Cancelled = 5,

    /// <summary>Reserved for v2 async health verification. Not written by v1.</summary>
    HealthChecking = 6,
}
