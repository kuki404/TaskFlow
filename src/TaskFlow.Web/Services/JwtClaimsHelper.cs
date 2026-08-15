using System.Text;
using System.Text.Json;

namespace TaskFlow.Web.Services;

/// <summary>
/// Decodes the JWT payload directly (base64url + JSON) instead of taking a dependency on
/// System.IdentityModel.Tokens.Jwt — the Web app only ever needs to read the "sub" claim to build
/// a display-only ClaimsPrincipal for Blazor's AuthorizeView; it never validates the token
/// (validation happens server-side, in TaskFlow.Api, on every API call).
/// </summary>
public static class JwtClaimsHelper
{
    public static Guid GetUserId(string accessToken)
    {
        var payload = accessToken.Split('.')[1];
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(Pad(payload)));
        using var document = JsonDocument.Parse(json);
        return Guid.Parse(document.RootElement.GetProperty("sub").GetString()!);
    }

    private static string Pad(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        return base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
    }
}
