namespace Cicd.Web.Admin.Services.Publisher;

/// <summary>
/// Options for the Publisher.Api typed HttpClient. Bound from configuration section
/// <c>"PublisherApi"</c>. <see cref="BaseUrl"/> defaults to the publisher dev port.
/// </summary>
public sealed class PublisherApiOptions
{
    public const string SectionName = "PublisherApi";

    public string BaseUrl { get; set; } = "http://localhost:9611";
}
