using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

/// <summary>Join entity: which users can access a project, and at what role (Owner/Member/Viewer). This — not TenantId membership — is what resource-based authorization checks against.</summary>
public class ProjectMember
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectRole Role { get; private set; }

    private ProjectMember()
    {
        // EF Core materialization constructor.
    }

    public static ProjectMember Create(Guid projectId, Guid userId, ProjectRole role) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        UserId = userId,
        Role = role
    };

    public void ChangeRole(ProjectRole role) => Role = role;
}
