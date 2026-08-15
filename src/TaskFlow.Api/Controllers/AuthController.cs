using TaskFlow.Api.Extensions;
using TaskFlow.Application.Dtos;
using TaskFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous] // the API is authenticated-by-default (see FallbackPolicy in Program.cs); auth itself must stay reachable
[EnableRateLimiting("auth")] // login/register/refresh are the highest-value brute-force targets — stricter than the global per-IP limit
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        if (!result.Succeeded)
        {
            // 401, not the generic Problem() mapping — a failed login is an authentication
            // failure, not a validation/conflict error, regardless of which branch failed inside the service.
            return Unauthorized(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(RefreshRequest request, CancellationToken ct)
    {
        await authService.RevokeAsync(request.RefreshToken, ct);
        return NoContent();
    }
}
