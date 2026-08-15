using TaskFlow.Domain.Common;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

/// <summary>
/// A Kanban column. Named "CardList" (not "List") to avoid clashing with System.Collections.Generic.List
/// throughout the codebase.
/// </summary>
public class CardList : IHasTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BoardId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Position { get; private set; }

    public List<Card> Cards { get; private set; } = [];

    private CardList()
    {
        // EF Core materialization constructor.
    }

    public static CardList Create(Guid tenantId, Guid boardId, string name, int position)
    {
        if (position < 0)
        {
            throw new DomainException("List position cannot be negative.");
        }

        return new CardList
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BoardId = boardId,
            Name = name.Trim(),
            Position = position
        };
    }

    public void Rename(string name) => Name = name.Trim();

    public void MoveTo(int position)
    {
        if (position < 0)
        {
            throw new DomainException("List position cannot be negative.");
        }

        Position = position;
    }
}
