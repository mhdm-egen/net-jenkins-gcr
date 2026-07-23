namespace Cicd.Web.Admin.Services.Sca;

/// <summary>
/// Explains a CVE in the context of the affected dependency, grounded in SBOM data and
/// cached. Soft-fails: <see cref="IsConfigured"/> is false when no AI key is set, and the
/// UI hides the action rather than erroring.
/// </summary>
public interface ICveExplainer
{
    bool IsConfigured { get; }

    Task<CveExplanation> ExplainAsync(CveExplainRequest request, CancellationToken ct = default);
}
