using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TaskFlow.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace TaskFlow.Infrastructure.Auth;

public class JwtTokenService(IConfiguration configuration) : ITokenService
{
    public AccessToken CreateAccessToken(TokenSubject subject)
    {
        var minutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var m) ? m : 15;
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(minutes);

        // "tenant_id" is the ONLY source TaskFlowDbContext's query filters trust (see
        // ICurrentTenantProvider) — it is set here, once, from the authenticated user's own
        // ApplicationUser.TenantId, and can never be supplied or overridden by the client.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, subject.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", subject.TenantId.ToString()),
            new("display_name", subject.DisplayName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSigningKey()));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    public (string RawToken, string TokenHash) CreateRefreshToken()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(rawBytes);
        return (rawToken, HashRefreshToken(rawToken));
    }

    public string HashRefreshToken(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(hashBytes);
    }

    private string GetSigningKey() =>
        configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException(
            "Jwt:Secret is not configured. Set it with 'dotnet user-secrets set \"Jwt:Secret\" \"<a long random string>\" --project src/TaskFlow.Api'.");
}
