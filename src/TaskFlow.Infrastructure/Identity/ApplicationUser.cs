using Microsoft.AspNetCore.Identity;

namespace TaskFlow.Infrastructure.Identity;

/// <summary>A user belongs to exactly one tenant — TenantId is set once at registration and never changes.</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
