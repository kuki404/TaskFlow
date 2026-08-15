using TaskFlow.Api.Authorization;
using TaskFlow.Api.Extensions;
using TaskFlow.Application.Common;
using TaskFlow.Application.Dtos;
using TaskFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController(IProjectService projectService, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectDto>>> GetMine([FromQuery] PagedRequest paging, CancellationToken ct)
    {
        return Ok(await projectService.GetForUserAsync(User.GetUserId(), paging, ct));
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var result = await projectService.CreateAsync(User.GetTenantId(), User.GetUserId(), request, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("{projectId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> GetMembers(Guid projectId, CancellationToken ct)
    {
        var authz = await authorizationService.AuthorizeAsync(User, projectId, PolicyNames.ProjectViewer);
        if (!authz.Succeeded)
        {
            return Forbid();
        }

        var result = await projectService.GetMembersAsync(projectId, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("{projectId:guid}/members")]
    public async Task<ActionResult<ProjectMemberDto>> AddMember(Guid projectId, AddProjectMemberRequest request, CancellationToken ct)
    {
        // Only an Owner manages membership — a Member/Viewer could otherwise grant themselves (or anyone else) Owner access.
        var authz = await authorizationService.AuthorizeAsync(User, projectId, PolicyNames.ProjectOwner);
        if (!authz.Succeeded)
        {
            return Forbid();
        }

        var result = await projectService.AddMemberAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPut("{projectId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> UpdateMemberRole(Guid projectId, Guid memberId, UpdateProjectMemberRoleRequest request, CancellationToken ct)
    {
        var authz = await authorizationService.AuthorizeAsync(User, projectId, PolicyNames.ProjectOwner);
        if (!authz.Succeeded)
        {
            return Forbid();
        }

        var result = await projectService.UpdateMemberRoleAsync(projectId, memberId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{projectId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid projectId, Guid memberId, CancellationToken ct)
    {
        var authz = await authorizationService.AuthorizeAsync(User, projectId, PolicyNames.ProjectOwner);
        if (!authz.Succeeded)
        {
            return Forbid();
        }

        var result = await projectService.RemoveMemberAsync(projectId, memberId, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken ct)
    {
        // Owner deletes project — resource-based check against real ProjectMember role, not just "is authenticated".
        var authz = await authorizationService.AuthorizeAsync(User, projectId, PolicyNames.ProjectOwner);
        if (!authz.Succeeded)
        {
            return Forbid();
        }

        var result = await projectService.DeleteAsync(projectId, ct);
        return result.ToActionResult(this);
    }
}
