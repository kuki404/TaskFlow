using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace TaskFlow.Web.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly AuthSession session;
    private bool restoreAttempted;

    public CustomAuthStateProvider(AuthSession session)
    {
        this.session = session;
        session.Changed += OnChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // The framework calls this during BOTH the static prerender pass (no JS interop yet — F5
        // persistence can't be read) and the later interactive pass on the same circuit (JS
        // interop is live). Retrying here means a page that survives to the interactive render
        // still recovers the session on F5, instead of only working for in-app navigation.
        if (!restoreAttempted && session.Current is null)
        {
            restoreAttempted = true;
            try
            {
                await session.RestoreAsync();
            }
            catch (InvalidOperationException)
            {
                // Prerendering — no circuit/JS interop yet. The interactive pass will retry.
                restoreAttempted = false;
            }
        }

        var auth = session.Current;
        if (auth is null)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, JwtClaimsHelper.GetUserId(auth.AccessToken).ToString()),
            new(ClaimTypes.Name, auth.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private void OnChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => session.Changed -= OnChanged;
}
