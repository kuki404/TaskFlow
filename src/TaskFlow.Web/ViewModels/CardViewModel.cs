using TaskFlow.Domain.Enums;

namespace TaskFlow.Web.ViewModels;

/// <summary>Web-only display fields (chip color) layered onto the API's CardDto via Mapster — kept out of the DTO itself, which has no business being coupled to MudBlazor's Color enum.</summary>
public class CardViewModel
{
    public Guid Id { get; set; }
    public Guid CardListId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CardPriority Priority { get; set; }
    public string? AssignedUserDisplayName { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public int Position { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public string PriorityColor => Priority switch
    {
        CardPriority.Urgent => "error",
        CardPriority.High => "warning",
        CardPriority.Medium => "info",
        _ => "default"
    };
}
