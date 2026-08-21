using TaskFlow.Application.Common;
using TaskFlow.Application.Dtos;

namespace TaskFlow.Application.Services;

public interface IBoardService
{
    Task<Result<BoardDto>> GetByIdAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task<Result<CardListDto>> CreateCardListAsync(Guid boardId, CreateCardListRequest request, CancellationToken cancellationToken = default);
    Task<Result> MoveCardListAsync(Guid boardId, Guid cardListId, MoveCardListRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteCardListAsync(Guid boardId, Guid cardListId, CancellationToken cancellationToken = default);

    /// <summary>Cross-board: every card assigned to this user across every project/board in their own tenant, overdue cards first.</summary>
    Task<IReadOnlyList<MyCardDto>> GetAssignedToUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<CardDto>> CreateCardAsync(Guid boardId, CreateCardRequest request, CancellationToken cancellationToken = default);
    Task<Result<CardDto>> UpdateCardAsync(Guid boardId, Guid cardId, UpdateCardRequest request, CancellationToken cancellationToken = default);
    Task<Result<CardDto>> MoveCardAsync(Guid boardId, Guid cardId, MoveCardRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteCardAsync(Guid boardId, Guid cardId, CancellationToken cancellationToken = default);

    /// <summary>Used by BoardHub before letting a caller join a board's SignalR group — a hub method is an endpoint, so this needs the same authorization as the REST API, not just group membership.</summary>
    Task<bool> IsProjectMemberForBoardAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Resolves the owning ProjectId for a board so BoardsController can run the same resource-based (ProjectRole) authorization checks ProjectsController uses, before this service ever executes a mutation.</summary>
    Task<Guid?> GetProjectIdForBoardAsync(Guid boardId, CancellationToken cancellationToken = default);
}
