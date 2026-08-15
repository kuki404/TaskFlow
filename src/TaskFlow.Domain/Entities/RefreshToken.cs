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

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    private RefreshToken()
    {
        // EF Core materialization constructor.
    }

    public static RefreshToken Create(Guid userId, string tokenHash, TimeSpan lifetime) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        CreatedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.Add(lifetime)
    };

    public void Revoke(Guid? replacedByTokenId = null)
    {
        RevokedAtUtc = DateTime.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }
}
