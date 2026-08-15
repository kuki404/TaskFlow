using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

/// <summary>Belongs to a Tenant, has exactly one Board, and a membership list controlling per-project RBAC (see ProjectMember).</summary>
public class Project : IHasTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Board? Board { get; private set; }
    public List<ProjectMember> Members { get; private set; } = [];

    private Project()
    {
        // EF Core materialization constructor.
    }

    public static Project Create(Guid tenantId, string name, string? description, Guid ownerUserId)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        project.Members.Add(ProjectMember.Create(project.Id, ownerUserId, Enums.ProjectRole.Owner));
        project.Board = Board.Create(tenantId, project.Id);

        return project;
    }
}
