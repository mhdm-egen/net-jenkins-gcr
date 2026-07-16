using Microsoft.JSInterop;
using Cicd.Web.Admin.Themes;

namespace Cicd.Web.Admin.Services;

/// <summary>
/// Per-circuit theme state shared between <c>MainLayout</c> (which owns the app-wide <c>MudThemeProvider</c>)
/// and the Settings page (which owns the theme selector). Persists the choice in browser localStorage so it
/// survives reloads. Scoped: one instance per Blazor Server circuit (per user connection).
/// </summary>
public sealed class ThemeService
{
    private const string StorageKey = "cicd-web-admin-theme";
    private readonly IJSRuntime _js;

    public ThemeService(IJSRuntime js) => _js = js;

    /// <summary>The active theme (name + MudTheme + dark flag). Falls back to the default until initialized.</summary>
    public AppThemes.Entry Current { get; private set; } = AppThemes.Resolve(AppThemes.SolarizedDarkId);

    /// <summary>Raised when the theme changes; subscribers re-render (the layout re-applies the provider).</summary>
    public event Action? Changed;

    /// <summary>Load the persisted theme from localStorage. Call once on first interactive render (JS isn't
    /// available during prerender).</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            var resolved = AppThemes.Resolve(stored);
            if (resolved.Id != Current.Id)
            {
                Current = resolved;
                Changed?.Invoke();
            }
        }
        catch (JSDisconnectedException) { /* prerender / page tearing down */ }
    }

    /// <summary>Set + persist the active theme and notify subscribers.</summary>
    public async Task SetAsync(string themeId)
    {
        Current = AppThemes.Resolve(themeId);
        Changed?.Invoke();
        try { await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, Current.Id); }
        catch (JSDisconnectedException) { /* page tearing down */ }
    }
}
