namespace TaskFlow.Domain.Entities;

/// <summary>An organization. Every user, project, board, list and card belongs to exactly one tenant.</summary>
public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private Tenant()
    {
        // EF Core materialization constructor.
    }

    public static Tenant Create(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        CreatedAtUtc = DateTime.UtcNow
    };
}
