using MudBlazor;
using MudBlazor.Utilities;

namespace Jenkins.WebUI.Themes;

/// <summary>
/// Named MudBlazor themes used by the app. Each theme is a self-contained
/// <see cref="MudTheme"/>; the active one is swapped wholesale and exposed
/// via <c>&lt;MudThemeProvider Theme="..." IsDarkMode="..." /&gt;</c>.
/// </summary>
public static class AppThemes
{
    public const string SolarizedDarkId  = "solarized-dark";
    public const string SolarizedLightId = "solarized-light";
    public const string DefaultId        = "default";

    public sealed record Entry(string Id, string Label, MudTheme Theme, bool IsDarkMode);

    public static IReadOnlyList<Entry> All { get; } = new[]
    {
        new Entry(SolarizedDarkId,  "Solarized Dark",  CreateSolarizedDark(),  IsDarkMode: true),
        new Entry(SolarizedLightId, "Solarized Light", CreateSolarizedLight(), IsDarkMode: false),
        new Entry(DefaultId,        "Default",         CreateDefault(),        IsDarkMode: false),
    };

    public static Entry Resolve(string? id) =>
        All.FirstOrDefault(e => e.Id == id) ?? All[0];

    // --- Solarized palette ---
    //   base03  #002b36   base02  #073642   base01  #586e75   base00  #657b83
    //   base0   #839496   base1   #93a1a1   base2   #eee8d5   base3   #fdf6e3
    //   yellow  #b58900   orange  #cb4b16   red     #dc322f   magenta #d33682
    //   violet  #6c71c4   blue    #268bd2   cyan    #2aa198   green   #859900

    private static MudTheme CreateSolarizedDark() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#268bd2",
            Secondary         = "#2aa198",
            Tertiary          = "#6c71c4",
            Info              = "#2aa198",
            Success           = "#859900",
            Warning           = "#b58900",
            Error             = "#dc322f",
            Black             = "#002b36",
            Background        = "#002b36",
            BackgroundGray    = "#073642",
            Surface           = "#073642",
            AppbarBackground  = "#073642",
            AppbarText        = "#93a1a1",
            DrawerBackground  = "#073642",
            DrawerText        = "#839496",
            DrawerIcon        = "#93a1a1",
            TextPrimary       = "#93a1a1",
            TextSecondary     = "#839496",
            TextDisabled      = "#586e75",
            ActionDefault     = "#839496",
            ActionDisabled    = "#586e75",
            ActionDisabledBackground = "#073642",
            LinesDefault      = "#073642",
            LinesInputs       = "#586e75",
            TableLines        = "#073642",
            TableStriped      = new MudColor("#073642").SetAlpha(0.40).ToString(),
            TableHover        = new MudColor("#586e75").SetAlpha(0.10).ToString(),
            Divider           = "#073642",
            DividerLight      = "#586e75",
        },
    };

    private static MudTheme CreateSolarizedLight() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary          = "#268bd2",
            Secondary        = "#2aa198",
            Tertiary         = "#6c71c4",
            Info             = "#2aa198",
            Success          = "#586e0d",
            Warning          = "#7d5e00",
            Error            = "#b32026",
            Background       = "#fdf6e3",
            BackgroundGray   = "#eee8d5",
            Surface          = "#eee8d5",
            AppbarBackground = "#eee8d5",
            AppbarText       = "#586e75",
            DrawerBackground = "#eee8d5",
            DrawerText       = "#657b83",
            DrawerIcon       = "#586e75",
            TextPrimary      = "#586e75",
            TextSecondary    = "#657b83",
            TextDisabled     = "#93a1a1",
            ActionDefault    = "#657b83",
            ActionDisabled   = "#93a1a1",
            ActionDisabledBackground = "#eee8d5",
            LinesDefault     = "#eee8d5",
            LinesInputs      = "#93a1a1",
            TableLines       = "#eee8d5",
            TableStriped     = new MudColor("#eee8d5").SetAlpha(0.50).ToString(),
            TableHover       = new MudColor("#93a1a1").SetAlpha(0.10).ToString(),
            Divider          = "#eee8d5",
            DividerLight     = "#93a1a1",
        },
    };

    private static MudTheme CreateDefault() => new();
}
