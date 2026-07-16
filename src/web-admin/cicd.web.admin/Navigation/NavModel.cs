using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace Cicd.Web.Admin.Navigation;

/// <summary>A single drawer nav entry. Internal links route within the app; <see cref="External"/> links open in a
/// new tab. <see cref="Match"/> mirrors the old markup's <c>NavLinkMatch</c> (Prefix everywhere except a couple of
/// exact "overview" links).</summary>
public sealed record NavEntry(
    string Label,
    string Href,
    string Icon,
    bool External = false,
    NavLinkMatch Match = NavLinkMatch.Prefix);

/// <summary>A drawer section (rendered as a <c>MudNavGroup</c>). <see cref="Key"/> is the stable id used to persist
/// the expand/collapse state and to match the active route (e.g. <c>"jenkins"</c>, nested <c>"cloud.google"</c>).
/// <see cref="Sub"/> holds one level of nested sub-sections (only Cloud → Google today).</summary>
public sealed record NavSection(
    string Key,
    string Title,
    string Icon,
    IReadOnlyList<NavEntry> Links,
    IReadOnlyList<NavSection> Sub);

/// <summary>
/// The admin drawer's navigation, expressed as data — the single source of truth for rendering the drawer, the
/// per-group expand-state keys (<see cref="NavSection.Key"/>), active-section detection (<see cref="ActiveKeys"/>),
/// and breadcrumbs (<see cref="Crumbs"/>). Reproduces the former hand-authored <c>MainLayout</c> nav exactly.
/// </summary>
public static class NavModel
{
    /// <summary>Top-level Home link (above the groups).</summary>
    public static readonly NavEntry Home =
        new("Home", "/", Icons.Material.Filled.Dashboard, Match: NavLinkMatch.All);

    /// <summary>Top-level Settings link (below the groups).</summary>
    public static readonly NavEntry Settings =
        new("Settings", "settings", Icons.Material.Filled.Settings);

    /// <summary>The ordered drawer sections. <paramref name="jenkinsAdminUrl"/> is the Jenkins server's own UI
    /// (runtime config, from <c>JenkinsOptions.BaseUrl</c>); the other external URLs are fixed host-reachable
    /// Nexus links, matching the previous markup.</summary>
    public static IReadOnlyList<NavSection> Build(string jenkinsAdminUrl) =>
    [
        new("jenkins", "Jenkins", Icons.Material.Filled.Build,
        [
            new("Orchestrator", "jenkins/orchestrator", Icons.Material.Filled.AccountTree),
            new("Pipelines",    "jenkins/pipelines",    Icons.Material.Filled.PlayArrow),
            new("Builds",       "jenkins/builds",       Icons.Material.Filled.History),
            new("Admin",        jenkinsAdminUrl,        Icons.Material.Filled.OpenInNew, External: true),
        ], []),
        new("nexus", "Nexus", Icons.Material.Filled.Inventory2,
        [
            new("Nuget",       "nexus/nuget",  Icons.Material.Filled.Inventory),
            new("Docker",      "nexus/docker", Icons.Material.Filled.ViewInAr),
            new("Admin",       "http://localhost:8081",                                Icons.Material.Filled.OpenInNew, External: true),
            new("Nuget Repo",  "http://localhost:8081/#browse/browse:nuget-hosted",    Icons.Material.Filled.FolderOpen, External: true),
            new("Docker Repo", "http://localhost:8081/#browse/browse:docker-private",  Icons.Material.Filled.FolderOpen, External: true),
            new("Sbom Repo",   "http://localhost:8081/#browse/browse:sboms",           Icons.Material.Filled.FolderOpen, External: true),
        ], []),
        new("sca", "SCA", Icons.Material.Filled.Analytics,
        [
            new("SBOM",        "sca/sbom",        Icons.Material.Filled.DataObject),
            new("Aspire SBOM", "sca/aspire-sbom", Icons.Material.Filled.Inventory2),
        ], []),
        new("ci", "CI", Icons.Material.Filled.Hub,
        [
            new("Repositories", "ci/repositories", Icons.Material.Filled.Source),
            new("Pipelines",    "ci/pipelines",    Icons.Material.Filled.AccountTree),
        ], []),
        new("deployment", "Deployment", Icons.Material.Filled.RocketLaunch,
        [
            new("Services",     "deployment/services",     Icons.Material.Filled.Apps),
            new("Environments", "deployment/environments", Icons.Material.Filled.Public),
            new("Mappings",     "deployment/mappings",     Icons.Material.Filled.Link),
            new("Inventory",    "deployment/containers",   Icons.Material.Filled.Inventory2),
            new("Aspire apps",  "deployment/aspire-apps",  Icons.Material.Filled.Hub),
            new("Previews",     "deployment/previews",     Icons.Material.Filled.Bolt),
            new("Deployments",  "deployment/runs",         Icons.Material.Filled.History),
            new("Metrics",      "deployment/metrics",      Icons.Material.Filled.Insights),
        ], []),
        new("kubernetes", "Kubernetes", Icons.Material.Filled.Dns,
        [
            new("Cluster",       "kubernetes/cluster", Icons.Material.Filled.AccountTree),
            new("Deployed apps", "kubernetes/apps",    Icons.Material.Filled.ViewList),
        ], []),
        new("cloud", "Cloud", Icons.Material.Filled.Cloud,
        [
            new("Azure", "cloud/azure", Icons.Material.Filled.Cloud),
        ],
        [
            new("cloud.google", "Google", Icons.Material.Filled.Cloud,
            [
                new("Overview",  "cloud/google",           Icons.Material.Filled.Dashboard, Match: NavLinkMatch.All),
                new("Artifacts", "cloud/google/artifacts", Icons.Material.Filled.Inventory2),
                new("Cloud Run", "cloud/google/cloud-run", Icons.Material.Filled.PlayCircleOutline),
            ], []),
        ]),
    ];

    /// <summary>The group key(s) to expand for a route: the matching top-level section, plus its nested sub-section
    /// key when the route lives under one (e.g. <c>["cloud", "cloud.google"]</c>). Empty for the home/aux routes.</summary>
    public static IReadOnlyList<string> ActiveKeys(string relativePath)
    {
        var path = Normalize(relativePath);
        if (path.Length == 0) return [];
        foreach (var s in Build(""))
        {
            foreach (var sub in s.Sub)
                if (BestLeaf(sub.Links, path) is not null)
                    return [s.Key, sub.Key];

            if (BestLeaf(s.Links, path) is not null || InSection(s, path))
                return [s.Key];
        }
        return [];
    }

    /// <summary>The breadcrumb trail for a route: <c>Home / Section [ / Sub ] / Page</c>. The section/sub/page crumbs
    /// are plain text (no index route); only <c>Home</c> is a link. Detail/aux routes with no exact nav leaf fall back
    /// to <c>Home / Section</c>. Empty (breadcrumb bar hidden) for <c>/</c> and unmatched routes (Error/NotFound).</summary>
    public static IReadOnlyList<(string Label, string? Href)> Crumbs(string relativePath)
    {
        var path = Normalize(relativePath);
        if (path.Length == 0) return [];
        foreach (var s in Build(""))
        {
            foreach (var sub in s.Sub)
                if (BestLeaf(sub.Links, path) is { } subLeaf)
                    return [("Home", "/"), (s.Title, null), (sub.Title, null), (subLeaf.Label, null)];

            if (BestLeaf(s.Links, path) is { } leaf)
                return [("Home", "/"), (s.Title, null), (leaf.Label, null)];

            if (InSection(s, path))
                return [("Home", "/"), (s.Title, null)];
        }
        return [];
    }

    // Whether the path lives under a section's route root (first segment of its first internal link) — used as the
    // fallback for detail/aux routes (e.g. /jenkins/builds/3, /jenkins/runs) that have no exact nav leaf.
    private static bool InSection(NavSection s, string path)
    {
        var root = SectionRoot(s);
        return root.Length > 0 && (path == root || path.StartsWith(root + "/", StringComparison.Ordinal));
    }

    private static string SectionRoot(NavSection s)
    {
        var first = s.Links.FirstOrDefault(l => !l.External)
                    ?? s.Sub.SelectMany(x => x.Links).FirstOrDefault(l => !l.External);
        var href = Normalize(first?.Href ?? "");
        var slash = href.IndexOf('/');
        return slash >= 0 ? href[..slash] : href;
    }

    // The internal link whose Href is the longest prefix of the path (so /jenkins/builds/3 resolves to the "Builds"
    // leaf). Null when no internal link matches.
    private static NavEntry? BestLeaf(IReadOnlyList<NavEntry> links, string path)
    {
        NavEntry? best = null;
        var bestLen = -1;
        foreach (var l in links)
        {
            if (l.External) continue;
            var h = Normalize(l.Href);
            if ((path == h || path.StartsWith(h + "/", StringComparison.Ordinal)) && h.Length > bestLen)
            {
                best = l;
                bestLen = h.Length;
            }
        }
        return best;
    }

    private static string Normalize(string p)
    {
        if (string.IsNullOrEmpty(p)) return string.Empty;
        var cut = p.IndexOfAny(['?', '#']);
        if (cut >= 0) p = p[..cut];
        return p.Trim('/').ToLowerInvariant();
    }
}
