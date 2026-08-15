using TaskFlow.Application.Common;
using TaskFlow.Application.Dtos;
using TaskFlow.Application.Mapping;
using TaskFlow.Application.Services;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Identity;
using TaskFlow.Infrastructure.Query;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Injects TaskFlowDbContext directly and projects straight to DTOs — no repository layer (see README).</summary>
public class ProjectService(TaskFlowDbContext db, UserManager<ApplicationUser> userManager) : IProjectService
{
    public async Task<PagedResult<ProjectDto>> GetForUserAsync(Guid userId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        // The tenant query filter already scopes this to the caller's own tenant; filtering to
        // projects the user is actually a member of keeps a tenant Owner's list from including
        // every project in the tenant, only the ones they belong to.
        var query = db.Projects
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .OrderBy(p => p.Name);

        return await query.ToPagedResultAsync(BoardProjections.ToProjectDto, paging, cancellationToken);
    }

    public async Task<Result<ProjectDto>> CreateAsync(Guid tenantId, Guid ownerUserId, CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = Project.Create(tenantId, request.Name, description: null, ownerUserId: ownerUserId);
        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        var dto = await db.Projects.AsNoTracking().Where(p => p.Id == project.Id).Select(BoardProjections.ToProjectDto).FirstAsync(cancellationToken);
        return Result<ProjectDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<ProjectMemberDto>>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var projectExists = await db.Projects.AsNoTracking().AnyAsync(p => p.Id == projectId, cancellationToken);
        if (!projectExists)
        {
            return Result<IReadOnlyList<ProjectMemberDto>>.Failure("Project not found.", ResultErrorType.NotFound);
        }

        var members = await db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new ProjectMemberDto(m.Id, u.Id, u.Email!, u.DisplayName, m.Role))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ProjectMemberDto>>.Success(members);
    }

    public async Task<Result<ProjectMemberDto>> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken = default)
    {
        var project = await db.Projects.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Result<ProjectMemberDto>.Failure("Project not found.", ResultErrorType.NotFound);
        }

        // The tenant query filter on ApplicationUser-adjacent lookups doesn't apply to Identity's
        // own DbSet, so this explicitly re-checks TenantId — a user from another tenant must never
        // be addable to this project, even if their email is somehow known.
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || user.TenantId != project.TenantId)
        {
            return Result<ProjectMemberDto>.Failure("No user with that email exists in this tenant.", ResultErrorType.Validation);
        }

        if (project.Members.Any(m => m.UserId == user.Id))
        {
            return Result<ProjectMemberDto>.Failure("User is already a member of this project.", ResultErrorType.Conflict);
        }

        var member = Domain.Entities.ProjectMember.Create(projectId, user.Id, request.Role);
        db.ProjectMembers.Add(member);
        await db.SaveChangesAsync(cancellationToken);

        return Result<ProjectMemberDto>.Success(new ProjectMemberDto(member.Id, user.Id, user.Email!, user.DisplayName, member.Role));
    }

    public async Task<Result> UpdateMemberRoleAsync(Guid projectId, Guid memberId, UpdateProjectMemberRoleRequest request, CancellationToken cancellationToken = default)
    {
        var member = await db.ProjectMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == projectId, cancellationToken);
        if (member is null)
        {
            return Result.Failure("Member not found.", ResultErrorType.NotFound);
        }

        member.ChangeRole(request.Role);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid projectId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var member = await db.ProjectMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == projectId, cancellationToken);
        if (member is null)
        {
            return Result.Failure("Member not found.", ResultErrorType.NotFound);
        }

        db.ProjectMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure("Project not found.", ResultErrorType.NotFound);
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
