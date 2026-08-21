using Shouldly;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.UnitTests;

public class CardTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CardListId = Guid.NewGuid();

    [Fact]
    public void Create_WithBlankTitle_Throws()
    {
        Should.Throw<DomainException>(() => Card.Create(TenantId, CardListId, "   ", null, CardPriority.Low, 0));
    }

    [Fact]
    public void Create_WithNegativePosition_Throws()
    {
        Should.Throw<DomainException>(() => Card.Create(TenantId, CardListId, "Title", null, CardPriority.Low, -1));
    }

    [Fact]
    public void Create_WithValidArgs_TrimsTitleAndDescription()
    {
        var card = Card.Create(TenantId, CardListId, "  Ship it  ", "  notes  ", CardPriority.High, 0);

        card.Title.ShouldBe("Ship it");
        card.Description.ShouldBe("notes");
        card.Priority.ShouldBe(CardPriority.High);
    }

    [Fact]
    public void MoveTo_ToNegativePosition_Throws()
    {
        var card = Card.Create(TenantId, CardListId, "Title", null, CardPriority.Low, 0);

        Should.Throw<DomainException>(() => card.MoveTo(Guid.NewGuid(), -1));
    }

    [Fact]
    public void MoveTo_ToAnotherList_UpdatesCardListIdAndPosition()
    {
        var card = Card.Create(TenantId, CardListId, "Title", null, CardPriority.Low, 0);
        var targetListId = Guid.NewGuid();

        card.MoveTo(targetListId, 3);

        card.CardListId.ShouldBe(targetListId);
        card.Position.ShouldBe(3);
    }

    [Fact]
    public void UpdateDetails_WithBlankTitle_Throws()
    {
        var card = Card.Create(TenantId, CardListId, "Title", null, CardPriority.Low, 0);

        Should.Throw<DomainException>(() => card.UpdateDetails("  ", null, CardPriority.Low, null, null));
    }

    [Fact]
    public void UpdateDetails_WithValidArgs_UpdatesFields()
    {
        var card = Card.Create(TenantId, CardListId, "Title", null, CardPriority.Low, 0);
        var assignee = Guid.NewGuid();
        var due = DateTime.UtcNow.AddDays(3);

        card.UpdateDetails("New title", "New description", CardPriority.Urgent, assignee, due);

        card.Title.ShouldBe("New title");
        card.Description.ShouldBe("New description");
        card.Priority.ShouldBe(CardPriority.Urgent);
        card.AssignedUserId.ShouldBe(assignee);
        card.DueDateUtc.ShouldBe(due);
    }
}
