using System.Text;

namespace Deployment.Application.Features.Previews;

/// <summary>Derives DNS-1123-safe slugs for preview keys and namespaces (lowercase alphanumerics + single
/// dashes, no leading/trailing dash, length-capped). Kubernetes namespaces must be a DNS-1123 label ≤ 63 chars.</summary>
internal static class PreviewNaming
{
    public static string SlugKey(string key) => Slug(key, 30);

    public static string Namespace(string appName, string keySlug)
    {
        var app = Slug(appName, 20);
        if (app.Length == 0) app = "app";
        var ns = $"{app}-preview-{keySlug}";
        return ns.Length <= 63 ? ns : ns[..63].TrimEnd('-');
    }

    private static string Slug(string s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return slug.Length <= max ? slug : slug[..max].TrimEnd('-');
    }
}
