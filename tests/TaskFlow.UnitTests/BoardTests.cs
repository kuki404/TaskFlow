using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.UnitTests;

public class BoardTests
{
    [Fact]
    public void Create_SeedsThreeDefaultLists()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var board = Board.Create(tenantId, projectId);

        Assert.Equal(3, board.CardLists.Count);
        Assert.Equal(["To Do", "In Progress", "Done"], board.CardLists.Select(l => l.Name));
        Assert.All(board.CardLists, l => Assert.Equal(tenantId, l.TenantId));
    }

    [Fact]
    public void Create_ListsHaveSequentialPositions()
    {
        var board = Board.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal([0, 1, 2], board.CardLists.Select(l => l.Position));
    }

    [Fact]
    public void CardListCreate_WithNegativePosition_Throws()
    {
        Assert.Throws<DomainException>(() => CardList.Create(Guid.NewGuid(), Guid.NewGuid(), "Backlog", -1));
    }

    [Fact]
    public void CardListMoveTo_WithNegativePosition_Throws()
    {
        var list = CardList.Create(Guid.NewGuid(), Guid.NewGuid(), "Backlog", 0);

        Assert.Throws<DomainException>(() => list.MoveTo(-1));
    }

    [Fact]
    public void ProjectCreate_AddsCreatorAsOwnerAndCreatesBoard()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var project = Project.Create(tenantId, "Launch", "desc", ownerId);

        Assert.NotNull(project.Board);
        Assert.Single(project.Members);
        Assert.Equal(ownerId, project.Members[0].UserId);
        Assert.Equal(ProjectRole.Owner, project.Members[0].Role);
    }
}
