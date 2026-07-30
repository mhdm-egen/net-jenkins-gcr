namespace Cicd.Web.Admin.Services.Sca;

/// <summary>
/// Turns <see cref="LicenseAnalyzer"/> findings into a shipping assessment, cached. Soft-fails:
/// <see cref="IsConfigured"/> is false with no AI key and the UI hides the action.
/// </summary>
public interface ILicenseExplainer
{
    bool IsConfigured { get; }

    Task<LicenseExplanation> ExplainAsync(LicenseExplainRequest request, CancellationToken ct = default);
}
