using TaskFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TaskFlow.Infrastructure.Hubs;

/// <summary>
/// Group name for a board's SignalR clients — one group per board, so a card update only
/// broadcasts to clients actually viewing that board.
/// </summary>
public static class BoardHubGroups
{
    public static string ForBoard(Guid boardId) => $"board-{boardId}";
}

/// <summary>
/// [Authorize] gets a valid JWT into HubCallerContext.User, but that alone does not prove the
/// caller may see THIS board — a hub method is an endpoint just like a controller action, so
/// JoinBoardAsync re-checks ProjectMember membership against the database before adding the
/// caller to the board's group. Joining a group is not itself authorization.
/// </summary>
[Authorize]
public class BoardHub(IBoardService boardService) : Hub
{
    public async Task JoinBoardAsync(Guid boardId)
    {
        var userId = GetUserId();
        if (userId is null || !await boardService.IsProjectMemberForBoardAsync(boardId, userId.Value, Context.ConnectionAborted))
        {
            throw new HubException("You are not a member of the project that owns this board.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, BoardHubGroups.ForBoard(boardId));
    }

    public async Task LeaveBoardAsync(Guid boardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BoardHubGroups.ForBoard(boardId));
    }

    private Guid? GetUserId()
    {
        var value = Context.User?.FindFirst("sub")?.Value ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
