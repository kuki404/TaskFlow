using TaskFlow.Application.Common;
using TaskFlow.Application.Dtos;

namespace TaskFlow.Application.Services;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> GetForUserAsync(Guid userId, PagedRequest paging, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> CreateAsync(Guid tenantId, Guid ownerUserId, CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectMemberDto>>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectMemberDto>> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateMemberRoleAsync(Guid projectId, Guid memberId, UpdateProjectMemberRoleRequest request, CancellationToken cancellationToken = default);
    Task<Result> RemoveMemberAsync(Guid projectId, Guid memberId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid projectId, CancellationToken cancellationToken = default);
}
