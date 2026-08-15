using TaskFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace TaskFlow.Api.Authorization;

/// <summary>Resource is the target Guid projectId; succeeds only if the caller's ProjectMember row for that project has at least MinimumRole (Viewer &lt; Member &lt; Owner, per the enum's declared order).</summary>
public class ProjectRoleRequirement(ProjectRole minimumRole) : IAuthorizationRequirement
{
    public ProjectRole MinimumRole { get; } = minimumRole;
}

public static class PolicyNames
{
    public const string ProjectViewer = "ProjectViewer";
    public const string ProjectMember = "ProjectMember";
    public const string ProjectOwner = "ProjectOwner";
}
