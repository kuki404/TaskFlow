using System.ComponentModel.DataAnnotations;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Dtos;

public record CreateProjectRequest([Required, MaxLength(200)] string Name);

public record ProjectDto(Guid Id, string Name, DateTime CreatedAtUtc, Guid BoardId, int MemberCount);

public record ProjectMemberDto(Guid Id, Guid UserId, string Email, string DisplayName, ProjectRole Role);

public record AddProjectMemberRequest([Required, EmailAddress] string Email, [Required] ProjectRole Role);

public record UpdateProjectMemberRoleRequest([Required] ProjectRole Role);
