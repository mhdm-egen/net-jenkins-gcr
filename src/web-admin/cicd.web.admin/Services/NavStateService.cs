using System.Text.Json;
using Microsoft.JSInterop;

namespace Cicd.Web.Admin.Services;

/// <summary>
/// Per-circuit memory of which drawer nav-groups are expanded, persisted to browser localStorage so the drawer
/// survives reloads. Groups default to collapsed; the layout auto-expands the active section on navigation.
/// Scoped: one instance per Blazor Server circuit. Mirrors <see cref="ThemeService"/>'s persistence pattern.
/// </summary>
public sealed class NavStateService
{
    private const string StorageKey = "cicd-web-admin-nav";
    private readonly IJSRuntime _js;
    private Dictionary<string, bool> _expanded = new();

    public NavStateService(IJSRuntime js) => _js = js;

    /// <summary>Raised when expansion state changes (a toggle, or the initial localStorage load); the layout re-renders.</summary>
    public event Action? Changed;

    /// <summary>Whether the group with this key is expanded. Groups default to collapsed.</summary>
    public bool IsExpanded(string key) => _expanded.GetValueOrDefault(key, false);

    /// <summary>Set + persist a group's expanded state and notify subscribers. No-op when unchanged (avoids
    /// a re-render/persist loop with the two-way <c>MudNavGroup.ExpandedChanged</c> binding).</summary>
    public void SetExpanded(string key, bool value)
    {
        if (_expanded.GetValueOrDefault(key, false) == value) return;
        _expanded[key] = value;
        Changed?.Invoke();
        _ = PersistAsync();
    }

    /// <summary>Load the persisted expansion map from localStorage. Call once on first interactive render (JS isn't
    /// available during prerender).</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(stored) &&
                JsonSerializer.Deserialize<Dictionary<string, bool>>(stored) is { } map)
            {
                _expanded = map;
                Changed?.Invoke();
            }
        }
        catch (JSDisconnectedException) { /* prerender / page tearing down */ }
        catch (JsonException) { /* corrupt value — start fresh */ }
    }

    private async Task PersistAsync()
    {
        try { await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(_expanded)); }
        catch (JSDisconnectedException) { /* page tearing down */ }
    }
}
