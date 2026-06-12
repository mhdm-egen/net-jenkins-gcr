using Publisher.Domain.Registries;

namespace Publisher.Application.Features.Promotions;

/// <summary>
/// Computes the destination image reference for a push. Default strategy: place the image under
/// the registry's repository path, preserving the container name, tagged with the source version
/// (falling back to <c>latest</c>). The image digest is preserved by the copy itself, so the tag
/// is purely a human-friendly handle.
/// </summary>
internal static class RemoteReference
{
    public static string BuildDestination(RemoteRegistry registry, string containerName, string version)
    {
        var tag = string.IsNullOrWhiteSpace(version) ? "latest" : SanitizeTag(version);
        var path = JoinPath(registry.RepositoryPath, containerName.Trim());
        return $"{registry.RegistryHost.TrimEnd('/')}/{path}:{tag}";
    }

    private static string JoinPath(string repositoryPath, string containerName)
    {
        var left = (repositoryPath ?? string.Empty).Trim().Trim('/');
        var right = containerName.Trim().Trim('/');
        return string.IsNullOrEmpty(left) ? right : $"{left}/{right}";
    }

    /// <summary>Docker tags allow [A-Za-z0-9_.-], max 128 chars; replace anything else with '-'.</summary>
    private static string SanitizeTag(string version)
    {
        var chars = version.Trim().Select(c =>
            (char.IsLetterOrDigit(c) || c is '_' or '.' or '-') ? c : '-').ToArray();
        var tag = new string(chars);
        return tag.Length > 128 ? tag[..128] : tag;
    }
}
