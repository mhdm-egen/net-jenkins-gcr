namespace Cicd.Web.Admin.Services.Metering;

/// <summary>
/// Points web-admin at the metering-api service. Bound from <c>Metering:Api</c>; the
/// BaseUrl is injected by the Aspire host / docker-compose. Empty => the HTTP usage
/// recorder no-ops (only the local OTel meter runs).
/// </summary>
public sealed record MeteringApiOptions
{
    public const string SectionName = "Metering:Api";

    public string BaseUrl { get; init; } = string.Empty;
}
