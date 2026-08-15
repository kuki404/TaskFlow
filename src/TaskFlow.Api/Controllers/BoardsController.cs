using TaskFlow.Api.Authorization;
using TaskFlow.Api.Extensions;
using TaskFlow.Application.Dtos;
using TaskFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/boards")]
[Authorize]
public class BoardsController(IBoardService boardService, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet("{boardId:guid}")]
    public async Task<ActionResult<BoardDto>> Get(Guid boardId, CancellationToken ct)
    {
        if (!await AuthorizeAsync(boardId, PolicyNames.ProjectViewer))
        {
            return Forbid();
        }

        var result = await boardService.GetByIdAsync(boardId, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("{boardId:guid}/lists")]
    public async Task<ActionResult<CardListDto>> CreateList(Guid boardId, CreateCardListRequest request, CancellationToken ct)
    {
        if (!await AuthorizeAsync(boardId, PolicyNames.ProjectMember))
        {
            return Forbid();
        }

        var result = await boardService.CreateCardListAsync(boardId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPut("{boardId:guid}/lists/{cardListId:guid}/move")]
    public async Task<IActionResult> MoveList(Guid boardId, Guid cardListId, MoveCardListRequest request, CancellationToken ct)
    {
        if (!await AuthorizeAsync(boardId, PolicyNames.ProjectMember))
        {
            return Forbid();
        }

        var result = await boardService.MoveCardListAsync(boardId, cardListId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{boardId:guid}/lists/{cardListId:guid}")]
    public async Task<IActionResult> DeleteList(Guid boardId, Guid cardListId, CancellationToken ct)
    {
        if (!await AuthorizeAsync(boardId, PolicyNames.ProjectMember))
        {
            return Forbid();
        }

        var result = await boardService.DeleteCardListAsync(boardId, cardListId, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("{boardId:guid}/cards")]
    public async Task<ActionResult<CardDto>> CreateCard(Guid boardId, CreateCardRequest request, CancellationToken ct)
    {
        // Member (or Owner) can CRUD cards — a Viewer is read-only, enforced here server-side
        // regardless of what the client sends.
        if (!await AuthorizeAsync(boardId, PolicyNames.ProjectMember))
        {
            return Forbid();
        }

        var result = await boardService.CreateCardAsync(boardId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPut("{boardId:guid}/cards/{cardId:guid}")]
    public async Task<ActionResult<CardDto>> UpdateCard(Guid boardId, Guid cardId, UpdateCardRequest request, CancellationToken ct)
    {
        if (!await AuthorizeAsync(boardId, PolicyNames.ProjectMember))
        {
            return Forbid();
        }

        var result = await boardService.UpdateCardAsync(boardId, cardId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPut("{boardId:guid}/cards/{cardId:guid}/move")]
    public async Task<ActionResult<CardDto>> MoveCard(Guid boardId, Guid cardId, MoveCardRequest request, CancellationToken ct)
    {
        if (!await AuthorizeAsync(boardId, PolicyNames.ProjectMember))
        {
            return Forbid();
        }

        var result = await boardService.MoveCardAsync(boardId, cardId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{boardId:guid}/cards/{cardId:guid}")]
    public async Task<IActionResult> DeleteCard(Guid boardId, Guid cardId, CancellationToken ct)
    {
        if (!await AuthorizeAsync(boardId, PolicyNames.ProjectMember))
        {
            return Forbid();
        }

        var result = await boardService.DeleteCardAsync(boardId, cardId, ct);
        return result.ToActionResult(this);
    }

    private async Task<bool> AuthorizeAsync(Guid boardId, string policy)
    {
        var projectId = await boardService.GetProjectIdForBoardAsync(boardId);
        if (projectId is null)
        {
            return false;
        }

        var authz = await authorizationService.AuthorizeAsync(User, projectId.Value, policy);
        return authz.Succeeded;
    }
}
