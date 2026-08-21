namespace TaskFlow.Domain.Entities;

/// <summary>Rotation + reuse-detection state for a single refresh token (see AuthController.Refresh).</summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    // Callers supply `now` (from TimeProvider) rather than this entity reading the clock itself —
    // keeps the clock a single injected dependency at the service boundary and makes reuse
    // detection / expiry precisely testable with a fake clock (see AuthService, RefreshTokenTests).
    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && nowUtc < ExpiresAtUtc;

    private RefreshToken()
    {
        // EF Core materialization constructor.
    }

    public static RefreshToken Create(Guid userId, string tokenHash, TimeSpan lifetime, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        CreatedAtUtc = nowUtc,
        ExpiresAtUtc = nowUtc.Add(lifetime)
    };

    public void Revoke(DateTime nowUtc, Guid? replacedByTokenId = null)
    {
        RevokedAtUtc = nowUtc;
        ReplacedByTokenId = replacedByTokenId;
    }
}
