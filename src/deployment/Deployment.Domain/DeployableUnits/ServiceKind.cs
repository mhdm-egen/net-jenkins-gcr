namespace Deployment.Domain.DeployableUnits;

/// <summary>
/// What kind of runnable artifact a <see cref="Service"/> produces. Drives some
/// deployment-target validation later (a WorkerService can't be hosted on a
/// static-site target, etc.). Extensible — add cases as new runtime kinds appear.
/// </summary>
public enum ServiceKind
{
    WebApi,
    Mvc,
    WorkerService,
    AzureFunction,
    Console,
    Other,
}
