using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace TaskFlow.Web.Services;

/// <summary>Dark/light preference, persisted across visits via ProtectedLocalStorage — defaults to following the OS preference (MudThemeProvider's own detection) until the user picks explicitly.</summary>
public class ThemeService(ProtectedLocalStorage storage)
{
    private const string StorageKey = "taskflow-dark-mode";

    public bool? IsDarkMode { get; private set; }

    public async Task LoadAsync()
    {
        var result = await storage.GetAsync<bool>(StorageKey);
        IsDarkMode = result.Success ? result.Value : null;
    }

    public async Task SetAsync(bool isDarkMode)
    {
        IsDarkMode = isDarkMode;
        await storage.SetAsync(StorageKey, isDarkMode);
    }
}
