using TaskFlow.Application.Common;
using TaskFlow.Application.Dtos;
using TaskFlow.Application.Mapping;
using TaskFlow.Application.Services;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Caching;
using TaskFlow.Infrastructure.Hubs;
using TaskFlow.Infrastructure.Query;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Injects TaskFlowDbContext directly — no repository layer. Publishes card mutations to BoardHub's SignalR group so every open board view stays live.</summary>
public class BoardService(TaskFlowDbContext db, BoardCache boardCache, IHubContext<BoardHub> hubContext) : IBoardService
{
    public async Task<Result<BoardDto>> GetByIdAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        // Board metadata (list names/positions) is cached; the compiled query below still runs on
        // a cache miss and loads cards fresh every time — only the shape of the columns is cached,
        // never card contents, which change far too often for a cache to help with.
        await boardCache.GetOrCreateAsync(boardId, async ct =>
        {
            var exists = await db.Boards.AsNoTracking().AnyAsync(b => b.Id == boardId, ct);
            return exists;
        }, cancellationToken);

        var board = await CompiledQueries.BoardByIdWithListsAndCards(db, boardId);
        if (board is null)
        {
            return Result<BoardDto>.Failure("Board not found.", ResultErrorType.NotFound);
        }

        var project = await db.Projects.AsNoTracking().Where(p => p.Id == board.ProjectId).Select(p => p.Name).FirstAsync(cancellationToken);
        var assigneeNames = await ResolveAssigneeNamesAsync(board.CardLists.SelectMany(l => l.Cards).Select(c => c.AssignedUserId), cancellationToken);

        var dto = new BoardDto(
            board.Id,
            board.ProjectId,
            project,
            board.CardLists.Select(l => new CardListDto(
                l.Id,
                l.Name,
                l.Position,
                l.Cards.Select(c => ToCardDto(c, assigneeNames)).ToList())).ToList());

        return Result<BoardDto>.Success(dto);
    }

    public async Task<Result<CardListDto>> CreateCardListAsync(Guid boardId, CreateCardListRequest request, CancellationToken cancellationToken = default)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board is null)
        {
            return Result<CardListDto>.Failure("Board not found.", ResultErrorType.NotFound);
        }

        var nextPosition = await db.CardLists.Where(l => l.BoardId == boardId).Select(l => (int?)l.Position).MaxAsync(cancellationToken) is { } max ? max + 1 : 0;
        var list = CardList.Create(board.TenantId, boardId, request.Name, nextPosition);
        db.CardLists.Add(list);
        await db.SaveChangesAsync(cancellationToken);
        await boardCache.InvalidateAsync(boardId, cancellationToken);

        await hubContext.Clients.Group(BoardHubGroups.ForBoard(boardId)).SendAsync("ListCreated", list.Id, list.Name, list.Position, cancellationToken);
        return Result<CardListDto>.Success(new CardListDto(list.Id, list.Name, list.Position, []));
    }

    public async Task<Result> MoveCardListAsync(Guid boardId, Guid cardListId, MoveCardListRequest request, CancellationToken cancellationToken = default)
    {
        var list = await db.CardLists.FirstOrDefaultAsync(l => l.Id == cardListId && l.BoardId == boardId, cancellationToken);
        if (list is null)
        {
            return Result.Failure("List not found.", ResultErrorType.NotFound);
        }

        list.MoveTo(request.Position);
        await db.SaveChangesAsync(cancellationToken);
        await boardCache.InvalidateAsync(boardId, cancellationToken);

        await hubContext.Clients.Group(BoardHubGroups.ForBoard(boardId)).SendAsync("ListMoved", cardListId, request.Position, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteCardListAsync(Guid boardId, Guid cardListId, CancellationToken cancellationToken = default)
    {
        var list = await db.CardLists.FirstOrDefaultAsync(l => l.Id == cardListId && l.BoardId == boardId, cancellationToken);
        if (list is null)
        {
            return Result.Failure("List not found.", ResultErrorType.NotFound);
        }

        db.CardLists.Remove(list);
        await db.SaveChangesAsync(cancellationToken);
        await boardCache.InvalidateAsync(boardId, cancellationToken);

        await hubContext.Clients.Group(BoardHubGroups.ForBoard(boardId)).SendAsync("ListDeleted", cardListId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<CardDto>> CreateCardAsync(Guid boardId, CreateCardRequest request, CancellationToken cancellationToken = default)
    {
        var list = await db.CardLists.FirstOrDefaultAsync(l => l.Id == request.CardListId && l.BoardId == boardId, cancellationToken);
        if (list is null)
        {
            return Result<CardDto>.Failure("List not found on this board.", ResultErrorType.NotFound);
        }

        var nextPosition = await db.Cards.Where(c => c.CardListId == list.Id).Select(c => (int?)c.Position).MaxAsync(cancellationToken) is { } max ? max + 1 : 0;
        var card = Card.Create(list.TenantId, list.Id, request.Title, request.Description, request.Priority, nextPosition);
        if (request.AssignedUserId is not null || request.DueDateUtc is not null)
        {
            card.UpdateDetails(request.Title, request.Description, request.Priority, request.AssignedUserId, request.DueDateUtc);
        }

        db.Cards.Add(card);
        await db.SaveChangesAsync(cancellationToken);

        var dto = await ToCardDtoWithAssigneeAsync(card, cancellationToken);
        await hubContext.Clients.Group(BoardHubGroups.ForBoard(boardId)).SendAsync("CardCreated", dto, cancellationToken);
        return Result<CardDto>.Success(dto);
    }

    public async Task<Result<CardDto>> UpdateCardAsync(Guid boardId, Guid cardId, UpdateCardRequest request, CancellationToken cancellationToken = default)
    {
        var card = await db.Cards.Join(db.CardLists, c => c.CardListId, l => l.Id, (c, l) => new { Card = c, l.BoardId })
            .Where(x => x.Card.Id == cardId && x.BoardId == boardId)
            .Select(x => x.Card)
            .FirstOrDefaultAsync(cancellationToken);

        if (card is null)
        {
            return Result<CardDto>.Failure("Card not found on this board.", ResultErrorType.NotFound);
        }

        // The client's RowVersion (captured when it last loaded the card) is what tells EF Core
        // whether it's still editing the same version of the row — a mismatch throws
        // DbUpdateConcurrencyException below, caught and turned into a typed conflict result.
        db.Entry(card).Property(c => c.RowVersion).OriginalValue = request.RowVersion;
        card.UpdateDetails(request.Title, request.Description, request.Priority, request.AssignedUserId, request.DueDateUtc);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CardDto>.Failure("This card was changed by someone else. Reload and try again.", ResultErrorType.Conflict);
        }

        var dto = await ToCardDtoWithAssigneeAsync(card, cancellationToken);
        await hubContext.Clients.Group(BoardHubGroups.ForBoard(boardId)).SendAsync("CardUpdated", dto, cancellationToken);
        return Result<CardDto>.Success(dto);
    }

    public async Task<Result<CardDto>> MoveCardAsync(Guid boardId, Guid cardId, MoveCardRequest request, CancellationToken cancellationToken = default)
    {
        var card = await db.Cards.Join(db.CardLists, c => c.CardListId, l => l.Id, (c, l) => new { Card = c, l.BoardId })
            .Where(x => x.Card.Id == cardId && x.BoardId == boardId)
            .Select(x => x.Card)
            .FirstOrDefaultAsync(cancellationToken);

        if (card is null)
        {
            return Result<CardDto>.Failure("Card not found on this board.", ResultErrorType.NotFound);
        }

        var targetListExists = await db.CardLists.AnyAsync(l => l.Id == request.TargetCardListId && l.BoardId == boardId, cancellationToken);
        if (!targetListExists)
        {
            return Result<CardDto>.Failure("Target list does not belong to this board.", ResultErrorType.Validation);
        }

        db.Entry(card).Property(c => c.RowVersion).OriginalValue = request.RowVersion;
        card.MoveTo(request.TargetCardListId, request.Position);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CardDto>.Failure("This card was changed by someone else. Reload and try again.", ResultErrorType.Conflict);
        }

        var dto = await ToCardDtoWithAssigneeAsync(card, cancellationToken);
        await hubContext.Clients.Group(BoardHubGroups.ForBoard(boardId)).SendAsync("CardMoved", dto, cancellationToken);
        return Result<CardDto>.Success(dto);
    }

    public async Task<Result> DeleteCardAsync(Guid boardId, Guid cardId, CancellationToken cancellationToken = default)
    {
        var card = await db.Cards.Join(db.CardLists, c => c.CardListId, l => l.Id, (c, l) => new { Card = c, l.BoardId })
            .Where(x => x.Card.Id == cardId && x.BoardId == boardId)
            .Select(x => x.Card)
            .FirstOrDefaultAsync(cancellationToken);

        if (card is null)
        {
            return Result.Failure("Card not found on this board.", ResultErrorType.NotFound);
        }

        db.Cards.Remove(card);
        await db.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(BoardHubGroups.ForBoard(boardId)).SendAsync("CardDeleted", cardId, cancellationToken);
        return Result.Success();
    }

    public async Task<bool> IsProjectMemberForBoardAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await db.Boards
            .Where(b => b.Id == boardId)
            .Join(db.ProjectMembers, b => b.ProjectId, m => m.ProjectId, (b, m) => m)
            .AnyAsync(m => m.UserId == userId, cancellationToken);
    }

    public async Task<Guid?> GetProjectIdForBoardAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        return await db.Boards.AsNoTracking().Where(b => b.Id == boardId).Select(b => (Guid?)b.ProjectId).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<CardDto> ToCardDtoWithAssigneeAsync(Card card, CancellationToken cancellationToken)
    {
        var names = await ResolveAssigneeNamesAsync([card.AssignedUserId], cancellationToken);
        return ToCardDto(card, names);
    }

    private static CardDto ToCardDto(Card card, IReadOnlyDictionary<Guid, string> assigneeNames) => new(
        card.Id,
        card.CardListId,
        card.Title,
        card.Description,
        card.Priority,
        card.AssignedUserId,
        card.AssignedUserId is { } id && assigneeNames.TryGetValue(id, out var name) ? name : null,
        card.DueDateUtc,
        card.Position,
        card.RowVersion);

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveAssigneeNamesAsync(IEnumerable<Guid?> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
    }
}
