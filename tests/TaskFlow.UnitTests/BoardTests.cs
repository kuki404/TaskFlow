using Shouldly;
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

        board.CardLists.Count.ShouldBe(3);
        board.CardLists.Select(l => l.Name).ShouldBe(["To Do", "In Progress", "Done"]);
        board.CardLists.ShouldAllBe(l => l.TenantId == tenantId);
    }

    [Fact]
    public void Create_ListsHaveSequentialPositions()
    {
        var board = Board.Create(Guid.NewGuid(), Guid.NewGuid());

        board.CardLists.Select(l => l.Position).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void CardListCreate_WithNegativePosition_Throws()
    {
        Should.Throw<DomainException>(() => CardList.Create(Guid.NewGuid(), Guid.NewGuid(), "Backlog", -1));
    }

    [Fact]
    public void CardListMoveTo_WithNegativePosition_Throws()
    {
        var list = CardList.Create(Guid.NewGuid(), Guid.NewGuid(), "Backlog", 0);

        Should.Throw<DomainException>(() => list.MoveTo(-1));
    }

    [Fact]
    public void ProjectCreate_AddsCreatorAsOwnerAndCreatesBoard()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var project = Project.Create(tenantId, "Launch", "desc", ownerId);

        project.Board.ShouldNotBeNull();
        project.Members.Count.ShouldBe(1);
        project.Members[0].UserId.ShouldBe(ownerId);
        project.Members[0].Role.ShouldBe(ProjectRole.Owner);
    }
}
