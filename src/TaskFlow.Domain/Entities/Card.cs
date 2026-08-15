using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

/// <summary>
/// Rich domain entity: no separate "status" field — a card's status IS which CardList it
/// currently belongs to, so moving a card between lists is the only state transition that
/// matters here.
/// </summary>
public class Card : IHasTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CardListId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public CardPriority Priority { get; private set; }
    public Guid? AssignedUserId { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public int Position { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>EF Core concurrency token (SQL Server rowversion) — protects against two users moving/editing the same card at once (see CardConfiguration.IsRowVersion()).</summary>
    public byte[] RowVersion { get; private set; } = [];

    private Card()
    {
        // EF Core materialization constructor.
    }

    public static Card Create(Guid tenantId, Guid cardListId, string title, string? description, CardPriority priority, int position)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Card title is required.");
        }

        if (position < 0)
        {
            throw new DomainException("Card position cannot be negative.");
        }

        return new Card
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CardListId = cardListId,
            Title = title.Trim(),
            Description = description?.Trim(),
            Priority = priority,
            Position = position,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdateDetails(string title, string? description, CardPriority priority, Guid? assignedUserId, DateTime? dueDateUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Card title is required.");
        }

        Title = title.Trim();
        Description = description?.Trim();
        Priority = priority;
        AssignedUserId = assignedUserId;
        DueDateUtc = dueDateUtc;
    }

    /// <summary>Moves the card to a (possibly different) list and position. Re-sequencing the rest of the source/target list's positions is the caller's (CardService) responsibility — this method only enforces the invariant on this card itself.</summary>
    public void MoveTo(Guid cardListId, int position)
    {
        if (position < 0)
        {
            throw new DomainException("Card position cannot be negative.");
        }

        CardListId = cardListId;
        Position = position;
    }
}
