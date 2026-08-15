namespace TaskFlow.Application.Abstractions;

public record TokenSubject(Guid UserId, Guid TenantId, string Email, string DisplayName);

public record AccessToken(string Value, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(TokenSubject subject);
    (string RawToken, string TokenHash) CreateRefreshToken();
    string HashRefreshToken(string rawToken);
}
