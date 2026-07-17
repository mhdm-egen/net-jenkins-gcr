using MudBlazor;
using MudBlazor.Utilities;

namespace Cicd.Web.Admin.Themes;

/// <summary>
/// Named MudBlazor themes used by the app. Each theme is a self-contained
/// <see cref="MudTheme"/>; the active one is swapped wholesale and exposed
/// via <c>&lt;MudThemeProvider Theme="..." IsDarkMode="..." /&gt;</c>.
/// </summary>
public static class AppThemes
{
    public const string SolarizedDarkId  = "solarized-dark";
    public const string SolarizedLightId = "solarized-light";
    public const string NordId           = "nord";
    public const string OneDarkId        = "one-dark";
    public const string MonokaiId        = "monokai";
    public const string MaterialDarkId   = "material-dark";
    public const string DraculaId        = "dracula";
    public const string TokyoNightId     = "tokyo-night";
    public const string CatppuccinId     = "catppuccin-mocha";
    public const string GithubDarkId     = "github-dark";
    public const string GruvboxDarkId    = "gruvbox-dark";
    public const string NightOwlId       = "night-owl";
    public const string DefaultId        = "default";

    public sealed record Entry(string Id, string Label, MudTheme Theme, bool IsDarkMode);

    public static IReadOnlyList<Entry> All { get; } = new[]
    {
        new Entry(SolarizedDarkId,  "Solarized Dark",  CreateSolarizedDark(),  IsDarkMode: true),
        new Entry(SolarizedLightId, "Solarized Light", CreateSolarizedLight(), IsDarkMode: false),
        new Entry(NordId,           "Nord",            CreateNord(),           IsDarkMode: true),
        new Entry(OneDarkId,        "One Dark",        CreateOneDark(),        IsDarkMode: true),
        new Entry(MonokaiId,        "Monokai",         CreateMonokai(),        IsDarkMode: true),
        new Entry(MaterialDarkId,   "Material Dark",   CreateMaterialDark(),   IsDarkMode: true),
        new Entry(DraculaId,        "Dracula",          CreateDracula(),          IsDarkMode: true),
        new Entry(TokyoNightId,     "Tokyo Night",      CreateTokyoNight(),       IsDarkMode: true),
        new Entry(CatppuccinId,     "Catppuccin Mocha", CreateCatppuccinMocha(),  IsDarkMode: true),
        new Entry(GithubDarkId,     "GitHub Dark",      CreateGithubDark(),       IsDarkMode: true),
        new Entry(GruvboxDarkId,    "Gruvbox Dark",     CreateGruvboxDark(),      IsDarkMode: true),
        new Entry(NightOwlId,       "Night Owl",        CreateNightOwl(),         IsDarkMode: true),
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

    // --- Nord palette (Arctic Ice Studio) ---
    //   Polar Night : nord0 #2e3440  nord1 #3b4252  nord2 #434c5e  nord3 #4c566a
    //   Snow Storm  : nord4 #d8dee9  nord5 #e5e9f0  nord6 #eceff4
    //   Frost       : nord7 #8fbcbb  nord8 #88c0d0  nord9 #81a1c1  nord10 #5e81ac
    //   Aurora      : nord11 #bf616a (red)  nord12 #d08770 (orange)
    //                 nord13 #ebcb8b (yellow)  nord14 #a3be8c (green)  nord15 #b48ead (purple)

    private static MudTheme CreateNord() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#88c0d0",   // nord8 — frost
            Secondary         = "#5e81ac",   // nord10 — frost deep
            Tertiary          = "#b48ead",   // nord15 — aurora purple
            Info              = "#8fbcbb",   // nord7
            Success           = "#a3be8c",   // nord14
            Warning           = "#ebcb8b",   // nord13
            Error             = "#bf616a",   // nord11
            Black             = "#2e3440",
            Background        = "#2e3440",   // nord0
            BackgroundGray    = "#3b4252",   // nord1
            Surface           = "#3b4252",
            AppbarBackground  = "#3b4252",
            AppbarText        = "#eceff4",   // nord6
            DrawerBackground  = "#3b4252",
            DrawerText        = "#d8dee9",   // nord4
            DrawerIcon        = "#eceff4",
            TextPrimary       = "#eceff4",
            TextSecondary     = "#d8dee9",
            TextDisabled      = "#4c566a",   // nord3
            ActionDefault     = "#d8dee9",
            ActionDisabled    = "#4c566a",
            ActionDisabledBackground = "#3b4252",
            LinesDefault      = "#434c5e",   // nord2
            LinesInputs       = "#4c566a",
            TableLines        = "#434c5e",
            TableStriped      = new MudColor("#434c5e").SetAlpha(0.40).ToString(),
            TableHover        = new MudColor("#4c566a").SetAlpha(0.15).ToString(),
            Divider           = "#434c5e",
            DividerLight      = "#4c566a",
        },
    };

    // --- One Dark palette (Atom) ---
    //   bg          #282c34   bg-darker   #21252b   surface     #3a3f4b
    //   fg          #abb2bf   fg-mid      #828997   fg-dim      #5c6370
    //   red #e06c75  orange #d19a66  yellow #e5c07b  green #98c379
    //   cyan #56b6c2  blue #61afef  purple #c678dd

    private static MudTheme CreateOneDark() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#61afef",   // blue
            Secondary         = "#56b6c2",   // cyan
            Tertiary          = "#c678dd",   // purple
            Info              = "#56b6c2",
            Success           = "#98c379",
            Warning           = "#e5c07b",
            Error             = "#e06c75",
            Black             = "#21252b",
            Background        = "#282c34",
            BackgroundGray    = "#21252b",
            Surface           = "#3a3f4b",
            AppbarBackground  = "#21252b",
            AppbarText        = "#abb2bf",
            DrawerBackground  = "#21252b",
            DrawerText        = "#abb2bf",
            DrawerIcon        = "#abb2bf",
            TextPrimary       = "#abb2bf",
            TextSecondary     = "#828997",
            TextDisabled      = "#5c6370",
            ActionDefault     = "#abb2bf",
            ActionDisabled    = "#5c6370",
            ActionDisabledBackground = "#21252b",
            LinesDefault      = "#3e4451",
            LinesInputs       = "#5c6370",
            TableLines        = "#3e4451",
            TableStriped      = new MudColor("#3e4451").SetAlpha(0.40).ToString(),
            TableHover        = new MudColor("#5c6370").SetAlpha(0.15).ToString(),
            Divider           = "#3e4451",
            DividerLight      = "#5c6370",
        },
    };

    // --- Monokai palette ---
    //   bg #272822  bg-lighter #3e3d32  selection #49483e
    //   fg #f8f8f2  fg-dim #75715e
    //   pink #f92672  orange #fd971f  yellow #e6db74  green #a6e22e
    //   cyan #66d9ef  purple #ae81ff

    private static MudTheme CreateMonokai() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#66d9ef",   // cyan — calm accent for buttons/links
            Secondary         = "#f92672",   // signature pink
            Tertiary          = "#ae81ff",   // purple
            Info              = "#66d9ef",
            Success           = "#a6e22e",
            Warning           = "#fd971f",   // orange — distinct from yellow #e6db74 to leave room for it
            Error             = "#f92672",
            Black             = "#272822",
            Background        = "#272822",
            BackgroundGray    = "#3e3d32",
            Surface            = "#3e3d32",
            AppbarBackground  = "#3e3d32",
            AppbarText        = "#f8f8f2",
            DrawerBackground  = "#3e3d32",
            DrawerText        = "#f8f8f2",
            DrawerIcon        = "#f8f8f2",
            TextPrimary       = "#f8f8f2",
            TextSecondary     = "#cfcfc2",
            TextDisabled      = "#75715e",
            ActionDefault     = "#f8f8f2",
            ActionDisabled    = "#75715e",
            ActionDisabledBackground = "#3e3d32",
            LinesDefault      = "#49483e",
            LinesInputs       = "#75715e",
            TableLines        = "#49483e",
            TableStriped      = new MudColor("#49483e").SetAlpha(0.40).ToString(),
            TableHover        = new MudColor("#75715e").SetAlpha(0.15).ToString(),
            Divider           = "#49483e",
            DividerLight      = "#75715e",
        },
    };

    // --- Material Dark palette (Material Design 2 dark spec) ---
    //   Background  #121212  Elevated surfaces ramp by overlay opacity
    //   Surface     #1e1e1e (≈ 1dp)   Surface higher #242424 (≈ 8dp)
    //   Primary     #bb86fc (purple 200)   Primary variant #3700b3
    //   Secondary   #03dac6 (teal 200)
    //   Error       #cf6679
    //   On-surface  #ffffff at 87% (primary text), 60% (secondary), 38% (disabled)

    private static MudTheme CreateMaterialDark() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#bb86fc",
            Secondary         = "#03dac6",
            Tertiary          = "#3700b3",
            Info              = "#03dac6",
            Success           = "#00c853",
            Warning           = "#ffb74d",
            Error             = "#cf6679",
            Black             = "#000000",
            Background        = "#121212",
            BackgroundGray    = "#1e1e1e",
            Surface           = "#1e1e1e",
            AppbarBackground  = "#242424",
            AppbarText        = new MudColor("#ffffff").SetAlpha(0.87).ToString(),
            DrawerBackground  = "#1e1e1e",
            DrawerText        = new MudColor("#ffffff").SetAlpha(0.87).ToString(),
            DrawerIcon        = new MudColor("#ffffff").SetAlpha(0.87).ToString(),
            TextPrimary       = new MudColor("#ffffff").SetAlpha(0.87).ToString(),
            TextSecondary     = new MudColor("#ffffff").SetAlpha(0.60).ToString(),
            TextDisabled      = new MudColor("#ffffff").SetAlpha(0.38).ToString(),
            ActionDefault     = new MudColor("#ffffff").SetAlpha(0.87).ToString(),
            ActionDisabled    = new MudColor("#ffffff").SetAlpha(0.38).ToString(),
            ActionDisabledBackground = "#1e1e1e",
            LinesDefault      = new MudColor("#ffffff").SetAlpha(0.12).ToString(),
            LinesInputs       = new MudColor("#ffffff").SetAlpha(0.38).ToString(),
            TableLines        = new MudColor("#ffffff").SetAlpha(0.12).ToString(),
            TableStriped      = new MudColor("#ffffff").SetAlpha(0.04).ToString(),
            TableHover        = new MudColor("#ffffff").SetAlpha(0.08).ToString(),
            Divider           = new MudColor("#ffffff").SetAlpha(0.12).ToString(),
            DividerLight      = new MudColor("#ffffff").SetAlpha(0.06).ToString(),
        },
    };

    // --- Dracula palette ---
    //   bg #282a36  current-line #44475a  fg #f8f8f2  comment #6272a4
    //   cyan #8be9fd  green #50fa7b  orange #ffb86c  pink #ff79c6
    //   purple #bd93f9  red #ff5555  yellow #f1fa8c

    private static MudTheme CreateDracula() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#bd93f9",   // purple — signature accent
            Secondary         = "#ff79c6",   // pink
            Tertiary          = "#8be9fd",   // cyan
            Info              = "#8be9fd",
            Success           = "#50fa7b",
            Warning           = "#ffb86c",   // orange (yellow #f1fa8c reads too light on chips)
            Error             = "#ff5555",
            Black             = "#21222c",
            Background        = "#282a36",
            BackgroundGray    = "#21222c",
            Surface           = "#343746",
            AppbarBackground  = "#21222c",
            AppbarText        = "#f8f8f2",
            DrawerBackground  = "#21222c",
            DrawerText        = "#f8f8f2",
            DrawerIcon        = "#f8f8f2",
            TextPrimary       = "#f8f8f2",
            TextSecondary     = "#bcc0d4",
            TextDisabled      = "#6272a4",
            ActionDefault     = "#f8f8f2",
            ActionDisabled    = "#6272a4",
            ActionDisabledBackground = "#21222c",
            LinesDefault      = "#44475a",
            LinesInputs       = "#6272a4",
            TableLines        = "#44475a",
            TableStriped      = new MudColor("#44475a").SetAlpha(0.40).ToString(),
            TableHover        = new MudColor("#6272a4").SetAlpha(0.15).ToString(),
            Divider           = "#44475a",
            DividerLight      = "#6272a4",
        },
    };

    // --- Tokyo Night palette (folke, "night") ---
    //   bg #1a1b26  bg_dark #16161e  bg_highlight #292e42  terminal_black #414868
    //   fg #c0caf5  fg_dark #a9b1d6  comment #565f89  surface #24283b
    //   blue #7aa2f7  cyan #7dcfff  green #9ece6a  magenta #bb9af7
    //   red #f7768e  yellow #e0af68  orange #ff9e64

    private static MudTheme CreateTokyoNight() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#7aa2f7",   // blue
            Secondary         = "#bb9af7",   // magenta/purple
            Tertiary          = "#7dcfff",   // cyan
            Info              = "#7dcfff",
            Success           = "#9ece6a",
            Warning           = "#e0af68",
            Error             = "#f7768e",
            Black             = "#16161e",
            Background        = "#1a1b26",
            BackgroundGray    = "#16161e",
            Surface           = "#24283b",
            AppbarBackground  = "#16161e",
            AppbarText        = "#c0caf5",
            DrawerBackground  = "#16161e",
            DrawerText        = "#a9b1d6",
            DrawerIcon        = "#c0caf5",
            TextPrimary       = "#c0caf5",
            TextSecondary     = "#a9b1d6",
            TextDisabled      = "#565f89",
            ActionDefault     = "#c0caf5",
            ActionDisabled    = "#565f89",
            ActionDisabledBackground = "#16161e",
            LinesDefault      = "#292e42",
            LinesInputs       = "#414868",
            TableLines        = "#292e42",
            TableStriped      = new MudColor("#292e42").SetAlpha(0.40).ToString(),
            TableHover        = new MudColor("#414868").SetAlpha(0.15).ToString(),
            Divider           = "#292e42",
            DividerLight      = "#414868",
        },
    };

    // --- Catppuccin Mocha palette ---
    //   base #1e1e2e  mantle #181825  crust #11111b  surface0 #313244
    //   surface1 #45475a  surface2 #585b70  overlay0 #6c7086
    //   text #cdd6f4  subtext0 #a6adc8  subtext1 #bac2de
    //   blue #89b4fa  sky #89dceb  teal #94e2d5  green #a6e3a1
    //   yellow #f9e2af  peach #fab387  red #f38ba8  mauve #cba6f7

    private static MudTheme CreateCatppuccinMocha() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#89b4fa",   // blue
            Secondary         = "#cba6f7",   // mauve
            Tertiary          = "#94e2d5",   // teal
            Info              = "#89dceb",   // sky
            Success           = "#a6e3a1",
            Warning           = "#f9e2af",
            Error             = "#f38ba8",
            Black             = "#11111b",
            Background        = "#1e1e2e",
            BackgroundGray    = "#181825",
            Surface           = "#313244",
            AppbarBackground  = "#181825",
            AppbarText        = "#cdd6f4",
            DrawerBackground  = "#181825",
            DrawerText        = "#bac2de",
            DrawerIcon        = "#cdd6f4",
            TextPrimary       = "#cdd6f4",
            TextSecondary     = "#a6adc8",
            TextDisabled      = "#6c7086",
            ActionDefault     = "#cdd6f4",
            ActionDisabled    = "#6c7086",
            ActionDisabledBackground = "#181825",
            LinesDefault      = "#45475a",
            LinesInputs       = "#585b70",
            TableLines        = "#45475a",
            TableStriped      = new MudColor("#313244").SetAlpha(0.50).ToString(),
            TableHover        = new MudColor("#585b70").SetAlpha(0.15).ToString(),
            Divider           = "#45475a",
            DividerLight      = "#585b70",
        },
    };

    // --- GitHub Dark palette (github.com dark default) ---
    //   canvas #0d1117  subtle #161b22  border #30363d  border-muted #21262d
    //   fg #e6edf3  fg-muted #8b949e  fg-subtle #6e7681
    //   accent #58a6ff  success #3fb950  attention #d29922  danger #f85149  done #a371f7

    private static MudTheme CreateGithubDark() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#58a6ff",   // accent blue
            Secondary         = "#a371f7",   // purple (done)
            Tertiary          = "#db61a2",   // pink (sponsors)
            Info              = "#58a6ff",
            Success           = "#3fb950",
            Warning           = "#d29922",
            Error             = "#f85149",
            Black             = "#010409",
            Background        = "#0d1117",
            BackgroundGray    = "#161b22",
            Surface           = "#161b22",
            AppbarBackground  = "#161b22",
            AppbarText        = "#e6edf3",
            DrawerBackground  = "#0d1117",
            DrawerText        = "#c9d1d9",
            DrawerIcon        = "#e6edf3",
            TextPrimary       = "#e6edf3",
            TextSecondary     = "#8b949e",
            TextDisabled      = "#6e7681",
            ActionDefault     = "#c9d1d9",
            ActionDisabled    = "#6e7681",
            ActionDisabledBackground = "#161b22",
            LinesDefault      = "#30363d",
            LinesInputs       = "#6e7681",
            TableLines        = "#30363d",
            TableStriped      = new MudColor("#161b22").SetAlpha(0.60).ToString(),
            TableHover        = new MudColor("#6e7681").SetAlpha(0.12).ToString(),
            Divider           = "#30363d",
            DividerLight      = "#21262d",
        },
    };

    // --- Gruvbox Dark palette (morhetz) ---
    //   bg0 #282828  bg0_h #1d2021  bg1 #3c3836  bg2 #504945  bg3 #665c54
    //   fg #ebdbb2  fg3 #bdae93  gray #928374
    //   blue #83a598  aqua #8ec07c  green #b8bb26  yellow #fabd2f
    //   orange #fe8019  red #fb4934  purple #d3869b

    private static MudTheme CreateGruvboxDark() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#83a598",   // blue — calm accent
            Secondary         = "#d3869b",   // purple
            Tertiary          = "#8ec07c",   // aqua
            Info              = "#8ec07c",
            Success           = "#b8bb26",
            Warning           = "#fabd2f",
            Error             = "#fb4934",
            Black             = "#1d2021",
            Background        = "#282828",
            BackgroundGray    = "#1d2021",
            Surface           = "#3c3836",
            AppbarBackground  = "#1d2021",
            AppbarText        = "#ebdbb2",
            DrawerBackground  = "#1d2021",
            DrawerText        = "#d5c4a1",
            DrawerIcon        = "#ebdbb2",
            TextPrimary       = "#ebdbb2",
            TextSecondary     = "#bdae93",
            TextDisabled      = "#928374",
            ActionDefault     = "#ebdbb2",
            ActionDisabled    = "#928374",
            ActionDisabledBackground = "#1d2021",
            LinesDefault      = "#504945",
            LinesInputs       = "#665c54",
            TableLines        = "#504945",
            TableStriped      = new MudColor("#3c3836").SetAlpha(0.50).ToString(),
            TableHover        = new MudColor("#665c54").SetAlpha(0.15).ToString(),
            Divider           = "#504945",
            DividerLight      = "#665c54",
        },
    };

    // --- Night Owl palette (sarah drasner) ---
    //   bg #011627  bg_light #0b2942  selection #1d3b53  fg #d6deeb  fg_dim #637777
    //   blue #82aaff  cyan #7fdbca  green #addb67  purple #c792ea
    //   red #ff5874  yellow #ecc48d  orange #f78c6c

    private static MudTheme CreateNightOwl() => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#82aaff",   // blue
            Secondary         = "#c792ea",   // purple
            Tertiary          = "#7fdbca",   // cyan
            Info              = "#7fdbca",
            Success           = "#addb67",
            Warning           = "#ecc48d",
            Error             = "#ff5874",
            Black             = "#01111d",
            Background        = "#011627",
            BackgroundGray    = "#0b2942",
            Surface           = "#0b2942",
            AppbarBackground  = "#01111d",
            AppbarText        = "#d6deeb",
            DrawerBackground  = "#01111d",
            DrawerText        = "#d6deeb",
            DrawerIcon        = "#d6deeb",
            TextPrimary       = "#d6deeb",
            TextSecondary     = "#8badc4",
            TextDisabled      = "#637777",
            ActionDefault     = "#d6deeb",
            ActionDisabled    = "#637777",
            ActionDisabledBackground = "#01111d",
            LinesDefault      = "#1d3b53",
            LinesInputs       = "#637777",
            TableLines        = "#1d3b53",
            TableStriped      = new MudColor("#0b2942").SetAlpha(0.40).ToString(),
            TableHover        = new MudColor("#637777").SetAlpha(0.15).ToString(),
            Divider           = "#1d3b53",
            DividerLight      = "#637777",
        },
    };

    private static MudTheme CreateDefault() => new();
}
