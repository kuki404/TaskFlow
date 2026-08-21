using TaskFlow.Api.Extensions;
using TaskFlow.Application.Dtos;
using TaskFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/cards")]
[Authorize]
public class CardsController(IBoardService boardService) : ControllerBase
{
    /// <summary>Every card assigned to the caller across every project/board — the tenant query
    /// filter (applied inside IBoardService.GetAssignedToUserAsync) already keeps this scoped to
    /// the caller's own tenant, and filtering by AssignedUserId == caller keeps it scoped to the
    /// caller's own cards, so no additional resource-based authorization check is needed here.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<MyCardDto>>> GetMine(CancellationToken ct)
    {
        var cards = await boardService.GetAssignedToUserAsync(User.GetUserId(), ct);
        return Ok(cards);
    }
}
