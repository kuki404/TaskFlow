using MudBlazor;

namespace TaskFlow.Web;

/// <summary>Custom violet/amber theme — deliberately distinct from BookIt's teal/indigo palette.</summary>
public static class AppTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#6D28D9",
            Secondary = "#F59E0B",
            AppbarBackground = "#6D28D9",
            Background = "#FAFAFA",
            Surface = "#FFFFFF"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#A78BFA",
            Secondary = "#FBBF24",
            AppbarBackground = "#1E1B29",
            Background = "#121016",
            Surface = "#1B1826"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        }
    };
}
