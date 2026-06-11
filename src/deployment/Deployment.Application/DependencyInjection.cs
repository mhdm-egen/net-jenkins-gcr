using Deployment.Application.Features.Catalog.Applications;
using Deployment.Application.Features.Catalog.ContainerImages;
using Deployment.Application.Features.Catalog.Services;
using Deployment.Application.Features.Configuration.CreateConfigurationSetting;
using Deployment.Application.Features.Configuration.DeleteConfigurationSetting;
using Deployment.Application.Features.Configuration.ListConfigurationSettings;
using Deployment.Application.Features.Configuration.ResolveEffectiveConfig;
using Deployment.Application.Features.Configuration.UpdateConfigurationSetting;
using Deployment.Application.Features.Environments.EditEnvironment;
using Deployment.Application.Features.Environments.ListEnvironments;
using Deployment.Application.Features.Environments.ManageFreezeWindows;
using Deployment.Application.Features.Environments.ManageTargets;
using Deployment.Application.Features.Environments.RegisterEnvironment;
using Deployment.Application.Features.Deployments.ApproveDeployment;
using Deployment.Application.Features.Deployments.BeginDeployment;
using Deployment.Application.Features.Deployments.CancelDeployment;
using Deployment.Application.Features.Deployments.FailDeployment;
using Deployment.Application.Features.Deployments.RecordDeploymentAudit;
using Deployment.Application.Features.Deployments.SucceedDeployment;
using Deployment.Application.Features.Deployments.GetDeploymentBaseline;
using Deployment.Application.Features.Deployments.GetEffectiveVersions;
using Deployment.Application.Features.Deployments.ListDeployments;
using Deployment.Application.Features.Deployments.StartDeployment;
using Deployment.Application.Features.Releases.AttachProvenance;
using Deployment.Application.Features.Releases.ChangeReleaseStatus;
using Deployment.Application.Features.Releases.ListReleases;
using Deployment.Application.Features.Releases.ManageComposition;
using Deployment.Application.Features.Releases.PublishRelease;
using Deployment.Application.Features.Releases.ResolveCompositionPins;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Deployment.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Application-layer DI registrations. Handlers are registered explicitly
    /// (one line per handler) rather than auto-scanned so the registration list
    /// reads as a catalog of capabilities. FluentValidation validators are
    /// scanned from the assembly.
    /// </summary>
    public static IServiceCollection AddDeploymentApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<DependencyInjectionMarker>(includeInternalTypes: true);

        // Catalog: Service handlers
        services.AddScoped<RegisterServiceHandler>();
        services.AddScoped<RenameServiceHandler>();
        services.AddScoped<UpdateServiceRepositoryInfoHandler>();
        services.AddScoped<ChangeServiceActivationHandler>();
        services.AddScoped<ListServicesHandler>();
        services.AddScoped<GetServiceByIdHandler>();

        // Catalog: ContainerImage handlers
        services.AddScoped<RegisterContainerImageHandler>();
        services.AddScoped<ChangeContainerImageDefaultTagHandler>();
        services.AddScoped<ChangeContainerImageActivationHandler>();
        services.AddScoped<ListContainerImagesHandler>();
        services.AddScoped<GetContainerImageByIdHandler>();
        services.AddScoped<ListContainerImageTagsHandler>();
        services.AddScoped<ResolveContainerImageHandler>();

        // Catalog: Application handlers
        services.AddScoped<RegisterApplicationHandler>();
        services.AddScoped<RenameApplicationHandler>();
        services.AddScoped<ChangeApplicationDescriptionHandler>();
        services.AddScoped<ChangeApplicationActivationHandler>();
        services.AddScoped<AddApplicationMemberHandler>();
        services.AddScoped<UpdateApplicationMemberHandler>();
        services.AddScoped<RemoveApplicationMemberHandler>();
        services.AddScoped<ListApplicationsHandler>();
        services.AddScoped<GetApplicationByIdHandler>();

        // Configuration handlers
        services.AddScoped<CreateConfigurationSettingHandler>();
        services.AddScoped<UpdateConfigurationSettingHandler>();
        services.AddScoped<DeleteConfigurationSettingHandler>();
        services.AddScoped<ListConfigurationSettingsByUnitHandler>();
        services.AddScoped<GetConfigurationSettingByIdHandler>();
        services.AddScoped<GetConfigurationSettingHistoryHandler>();

        // Environment handlers
        services.AddScoped<RegisterEnvironmentHandler>();
        services.AddScoped<RenameEnvironmentHandler>();
        services.AddScoped<ChangePromotionRankHandler>();
        services.AddScoped<SetApprovalRequirementHandler>();
        services.AddScoped<SetProductionFlagHandler>();
        services.AddScoped<AddTargetHandler>();
        services.AddScoped<UpdateTargetHandler>();
        services.AddScoped<RemoveTargetHandler>();
        services.AddScoped<ScheduleFreezeWindowHandler>();
        services.AddScoped<CancelFreezeWindowHandler>();
        services.AddScoped<ListEnvironmentsHandler>();
        services.AddScoped<GetEnvironmentByIdHandler>();

        // Release lifecycle handlers
        services.AddScoped<PublishReleaseHandler>();
        services.AddScoped<AttachProvenanceHandler>();
        services.AddScoped<ChangeReleaseStatusHandler>();
        services.AddScoped<AddCompositionEntryHandler>();
        services.AddScoped<UpdateCompositionEntryHandler>();
        services.AddScoped<RemoveCompositionEntryHandler>();
        services.AddScoped<ListReleasesByUnitHandler>();
        services.AddScoped<GetReleaseByIdHandler>();
        services.AddScoped<GetReleaseStatusHistoryHandler>();

        // Workflow handlers
        services.AddScoped<ResolveCompositionPinsHandler>();
        services.AddScoped<ResolveEffectiveConfigHandler>();
        services.AddScoped<ApproveDeploymentHandler>();
        services.AddScoped<StartDeploymentHandler>();
        services.AddScoped<CancelDeploymentHandler>();
        services.AddScoped<BeginDeploymentHandler>();
        services.AddScoped<SucceedDeploymentHandler>();
        services.AddScoped<FailDeploymentHandler>();
        services.AddScoped<RecordDeploymentAuditHandler>();
        services.AddScoped<ListDeploymentsHandler>();
        services.AddScoped<GetDeploymentByIdHandler>();
        services.AddScoped<GetEffectiveVersionsHandler>();
        services.AddScoped<GetDeploymentBaselineHandler>();

        return services;
    }

    /// <summary>
    /// Empty marker — gives FluentValidation an assembly anchor without
    /// exposing a real type from this layer.
    /// </summary>
    internal sealed class DependencyInjectionMarker { }
}
