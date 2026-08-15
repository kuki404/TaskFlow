namespace TaskFlow.Domain.Enums;

/// <summary>Per-project membership role — deliberately not tenant-wide, so the same user can be an Owner on one project and a Viewer on another.</summary>
public enum ProjectRole
{
    Viewer = 0,
    Member = 1,
    Owner = 2
}
