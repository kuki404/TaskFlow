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
        Assert.Throws<DomainException>(() => Card.Create(TenantId, CardListId, "   ", null, CardPriority.Low, 0));
    }

    [Fact]
    public void Create_WithNegativePosition_Throws()
    {
        Assert.Throws<DomainException>(() => Card.Create(TenantId, CardListId, "Title", null, CardPriority.Low, -1));
    }

    [Fact]
    public void Create_WithValidArgs_TrimsTitleAndDescription()
    {
        var card = Card.Create(TenantId, CardListId, "  Ship it  ", "  notes  ", CardPriority.High, 0);

        Assert.Equal("Ship it", card.Title);
        Assert.Equal("notes", card.Description);
        Assert.Equal(CardPriority.High, card.Priority);
    }

    [Fact]
    public void MoveTo_ToNegativePosition_Throws()
    {
        var card = Card.Create(TenantId, CardListId, "Title", null, CardPriority.Low, 0);

        Assert.Throws<DomainException>(() => card.MoveTo(Guid.NewGuid(), -1));
    }

    [Fact]
    public void MoveTo_ToAnotherList_UpdatesCardListIdAndPosition()
    {
        var card = Card.Create(TenantId, CardListId, "Title", null, CardPriority.Low, 0);
        var targetListId = Guid.NewGuid();

        card.MoveTo(targetListId, 3);

        Assert.Equal(targetListId, card.CardListId);
        Assert.Equal(3, card.Position);
    }

    [Fact]
    public void UpdateDetails_WithBlankTitle_Throws()
    {
        var card = Card.Create(TenantId, CardListId, "Title", null, CardPriority.Low, 0);

        Assert.Throws<DomainException>(() => card.UpdateDetails("  ", null, CardPriority.Low, null, null));
    }

    [Fact]
    public void UpdateDetails_WithValidArgs_UpdatesFields()
    {
        var card = Card.Create(TenantId, CardListId, "Title", null, CardPriority.Low, 0);
        var assignee = Guid.NewGuid();
        var due = DateTime.UtcNow.AddDays(3);

        card.UpdateDetails("New title", "New description", CardPriority.Urgent, assignee, due);

        Assert.Equal("New title", card.Title);
        Assert.Equal("New description", card.Description);
        Assert.Equal(CardPriority.Urgent, card.Priority);
        Assert.Equal(assignee, card.AssignedUserId);
        Assert.Equal(due, card.DueDateUtc);
    }
}
