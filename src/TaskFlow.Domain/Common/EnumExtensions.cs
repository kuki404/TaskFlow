using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Common;

public static class EnumExtensions
{
    public static string ToDisplayText(this CardPriority priority) => priority switch
    {
        CardPriority.Low => "Low",
        CardPriority.Medium => "Medium",
        CardPriority.High => "High",
        CardPriority.Urgent => "Urgent",
        _ => priority.ToString()
    };

    public static string ToDisplayText(this ProjectRole role) => role switch
    {
        ProjectRole.Owner => "Owner",
        ProjectRole.Member => "Member",
        ProjectRole.Viewer => "Viewer",
        _ => role.ToString()
    };
}
