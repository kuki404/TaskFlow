using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace TaskFlow.Web.Services;

/// <summary>Dark/light preference, persisted across visits via ProtectedLocalStorage — defaults to following the OS preference (MudThemeProvider's own detection) until the user picks explicitly.</summary>
public class ThemeService(ProtectedLocalStorage storage)
{
    private const string StorageKey = "taskflow-dark-mode";

    public bool? IsDarkMode { get; private set; }

    public async Task LoadAsync()
    {
        // Unlike ProtectedSessionStorage, this value outlives a server restart (it's browser
        // localStorage) — so it's the more likely of the two to be encrypted under a Data
        // Protection key the app no longer has after a redeploy with no persisted key ring. An
        // undecryptable stored preference just means "no preference recorded", not a crash.
        try
        {
            var result = await storage.GetAsync<bool>(StorageKey);
            IsDarkMode = result.Success ? result.Value : null;
        }
        catch (CryptographicException)
        {
            IsDarkMode = null;
        }
    }

    public async Task SetAsync(bool isDarkMode)
    {
        IsDarkMode = isDarkMode;
        await storage.SetAsync(StorageKey, isDarkMode);
    }
}
