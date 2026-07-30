namespace Cicd.Web.Admin.Services.Metering;

/// <summary>
/// An AI spend budget for the current calendar month.
///
/// Deliberately **advisory only**. It shows progress and warns; it does not block a call, and
/// nothing in the AI layer consults it. A budget that silently disabled features would turn a cost
/// question into an availability incident, and the person who set it is not necessarily the person
/// mid-way through triaging a failed deploy.
///
/// It is also deliberately **config, not storage** — one number per environment, with no entity, no
/// migration, and no admin screen to keep in sync.
/// </summary>
public sealed class BudgetOptions
{
    public const string SectionName = "Metering:Budget";

    /// <summary>
    /// Monthly AI spend budget in USD. Null (the default) hides the budget UI entirely — an
    /// unset budget shows nothing rather than a zero one, which would read as "over budget".
    /// </summary>
    public decimal? MonthlyUsd { get; set; }

    /// <summary>Fraction of the budget at which the display turns from informational to warning.</summary>
    public double WarnAtFraction { get; set; } = 0.8;

    public bool IsConfigured => MonthlyUsd is > 0m;
}
