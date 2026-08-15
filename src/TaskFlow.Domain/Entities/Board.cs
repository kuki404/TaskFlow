using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

/// <summary>One board per Project, holding an ordered set of CardLists (columns).</summary>
public class Board : IHasTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public List<CardList> CardLists { get; private set; } = [];

    private Board()
    {
        // EF Core materialization constructor.
    }

    public static Board Create(Guid tenantId, Guid projectId)
    {
        var board = new Board
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Every new board starts with a conventional three-column Kanban layout so the UI is
        // never an empty board with nowhere to put a card.
        board.CardLists.Add(CardList.Create(tenantId, board.Id, "To Do", 0));
        board.CardLists.Add(CardList.Create(tenantId, board.Id, "In Progress", 1));
        board.CardLists.Add(CardList.Create(tenantId, board.Id, "Done", 2));

        return board;
    }
}
