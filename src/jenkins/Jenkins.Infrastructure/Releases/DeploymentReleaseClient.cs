using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Deployment.Contracts.Releases;
using Jenkins.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Jenkins.Infrastructure.Releases;

/// <summary>
/// Typed-HttpClient adapter that fulfils <see cref="IDeploymentReleaseClient"/> by
/// calling the deployment microservice's Releases API (handoff §7). Maps the
/// CI-local inputs to <c>Deployment.Contracts</c> wire types. Enums are sent as
/// strings to match the deployment API's JSON shape.
/// </summary>
public sealed class DeploymentReleaseClient : IDeploymentReleaseClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;
    private readonly ILogger<DeploymentReleaseClient> _logger;

    public DeploymentReleaseClient(HttpClient http, ILogger<DeploymentReleaseClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<Guid> PublishContainerReleaseAsync(PublishReleaseInput input, CancellationToken ct = default)
    {
        var request = new PublishReleaseRequest(
            DeployableUnitId: input.DeployableUnitId,
            SemanticVersion: input.SemanticVersion,
            BuildNumber: input.BuildNumber,
            CommitSha: input.CommitSha,
            ArtifactType: ArtifactTypeDto.ContainerImage,
            ArtifactUri: input.ArtifactUri);

        using var response = await _http.PostAsJsonAsync("/api/deployment/releases", request, Json, ct)
            .ConfigureAwait(false);

        // Decision #4: a duplicate (DeployableUnitId, SemanticVersion) means the
        // release already exists. The publish endpoint doesn't echo the id on 409,
        // so surface it distinctly for the handoff handler to resolve via lookup.
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new DeploymentReleaseConflictException(input.DeployableUnitId, input.SemanticVersion);

        await EnsureSuccessAsync(response, "publish release", ct).ConfigureAwait(false);

        var created = await response.Content.ReadFromJsonAsync<CreatedIdResponse>(Json, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Publish release returned an empty body.");
        return created.Id;
    }

    public async Task AttachProvenanceAsync(Guid releaseId, AttachProvenanceInput input, CancellationToken ct = default)
    {
        var request = new AttachProvenanceRequest(
            ArtifactSha256: input.ArtifactSha256,
            SbomUri: input.SbomUri,
            VulnerabilityReportUri: input.VulnerabilityReportUri,
            CiRunUrl: input.CiRunUrl,
            CiRunId: input.CiRunId,
            PublishedByPrincipal: input.PublishedByPrincipal);

        using var response = await _http
            .PostAsJsonAsync($"/api/deployment/releases/{releaseId}/provenance", request, Json, ct)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, "attach provenance", ct).ConfigureAwait(false);
    }

    public async Task<Guid?> GetReleaseIdByVersionAsync(Guid deployableUnitId, string semanticVersion, CancellationToken ct = default)
    {
        using var response = await _http
            .GetAsync($"/api/deployment/releases?deployableUnitId={deployableUnitId}", ct)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var releases = await response.Content.ReadFromJsonAsync<List<ReleaseRef>>(Json, ct).ConfigureAwait(false)
            ?? new List<ReleaseRef>();
        return releases
            .FirstOrDefault(r => string.Equals(r.SemanticVersion, semanticVersion, StringComparison.Ordinal))?.Id;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _logger.LogError("[handoff] {Action} failed: HTTP {Status}. {Body}", action, (int)response.StatusCode, body);
        throw new HttpRequestException($"Deployment API {action} failed: HTTP {(int)response.StatusCode}. {body}");
    }

    private sealed record CreatedIdResponse(Guid Id);

    // Minimal subset of the deployment ReleaseDto — extra fields are ignored.
    private sealed record ReleaseRef(Guid Id, string SemanticVersion);
}
