using TaskFlow.Api.Extensions;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Api.Authorization;

/// <summary>
/// Resource-based authorization checking the caller's REAL ProjectMember role for the specific
/// project (resource is the projectId Guid) — never just "are they authenticated" or "do they
/// hold some role generally". Enforced server-side on every mutating endpoint; a Viewer can never
/// CRUD a card no matter what the client sends, and only an Owner can delete a project.
/// </summary>
public class ProjectRoleAuthorizationHandler(TaskFlowDbContext db) : AuthorizationHandler<ProjectRoleRequirement, Guid>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ProjectRoleRequirement requirement, Guid projectId)
    {
        var userId = context.User.GetUserId();

        var role = await db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .Select(m => (ProjectRole?)m.Role)
            .FirstOrDefaultAsync();

        // ProjectRole's declared numeric order (Viewer=0 < Member=1 < Owner=2) makes ">=" a valid
        // "at least this privileged" check without a separate ranking table.
        if (role is not null && role >= requirement.MinimumRole)
        {
            context.Succeed(requirement);
        }
    }
}
