using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Common;
using TaskFlow.Application.Dtos;
using TaskFlow.Application.Services;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Mirrors BookIt's AuthController pattern: lockout via CheckPasswordSignInAsync, refresh-token rotation with reuse detection, both carrying the tenant claim.</summary>
public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TaskFlowDbContext db,
    ITokenService tokenService) : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // A registration always creates a brand-new tenant — TaskFlow has no "join an existing
        // org by email domain" flow; existing tenants grow via ProjectService.AddMemberAsync
        // instead, which never lets a caller pick their own TenantId.
        var tenant = Tenant.Create(request.TenantName);
        db.Tenants.Add(tenant);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            TenantId = tenant.Id,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            // A rejected password is not an enumeration oracle — telling the user which rule
            // their password failed reveals nothing about whether the email is already
            // registered. Only collapse to the generic message when a duplicate-email error is
            // among the failures, so response specificity can never confirm an existing account.
            var isDuplicateEmail = createResult.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail");
            var message = isDuplicateEmail
                ? "Could not create an account with the provided details."
                : string.Join(" ", createResult.Errors.Select(e => e.Description));

            return Result<AuthResponse>.Failure(message);
        }

        return Result<AuthResponse>.Success(await IssueTokensAsync(user, cancellationToken));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Same response as a wrong password — an "unknown email" response would let an
            // attacker enumerate accounts one guess at a time.
            return Result<AuthResponse>.Failure("Invalid email or password.");
        }

        // CheckPasswordSignInAsync (not a plain password check) is what actually counts failed
        // attempts and locks the account — brute-force protection lives here, not in the DB.
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Result<AuthResponse>.Failure("Too many failed attempts. Try again later.", ResultErrorType.Conflict);
        }

        if (!result.Succeeded)
        {
            return Result<AuthResponse>.Failure("Invalid email or password.");
        }

        return Result<AuthResponse>.Success(await IssueTokensAsync(user, cancellationToken));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(rawRefreshToken);
        var storedToken = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return Result<AuthResponse>.Failure("Refresh token is invalid or expired.", ResultErrorType.NotFound);
        }

        if (storedToken.RevokedAtUtc is not null)
        {
            // Reuse of an already-revoked token means a captured/stolen token was replayed after
            // the legitimate rotation already moved past it — kill every active session for the
            // account, not just this one token (OWASP / OAuth 2.0 Security BCP reuse detection).
            await db.RefreshTokens
                .Where(t => t.UserId == storedToken.UserId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAtUtc, DateTime.UtcNow), cancellationToken);

            return Result<AuthResponse>.Failure("Refresh token has already been used. All sessions were revoked.", ResultErrorType.Conflict);
        }

        if (!storedToken.IsActive)
        {
            return Result<AuthResponse>.Failure("Refresh token is invalid or expired.", ResultErrorType.NotFound);
        }

        var user = await userManager.FindByIdAsync(storedToken.UserId.ToString());
        if (user is null)
        {
            return Result<AuthResponse>.Failure("User no longer exists.", ResultErrorType.NotFound);
        }

        var (response, newTokenEntity) = await IssueTokensWithEntityAsync(user, cancellationToken);
        storedToken.Revoke(newTokenEntity.Id);
        await db.SaveChangesAsync(cancellationToken);

        return Result<AuthResponse>.Success(response);
    }

    public async Task RevokeAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(rawRefreshToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAtUtc, DateTime.UtcNow), cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        (await IssueTokensWithEntityAsync(user, cancellationToken)).Response;

    private async Task<(AuthResponse Response, RefreshToken Entity)> IssueTokensWithEntityAsync(
        ApplicationUser user, CancellationToken cancellationToken)
    {
        var accessToken = tokenService.CreateAccessToken(new TokenSubject(user.Id, user.TenantId, user.Email!, user.DisplayName));

        var (rawRefreshToken, refreshTokenHash) = tokenService.CreateRefreshToken();
        var refreshTokenEntity = RefreshToken.Create(user.Id, refreshTokenHash, RefreshTokenLifetime);
        db.RefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync(cancellationToken);

        var response = new AuthResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            rawRefreshToken,
            refreshTokenEntity.ExpiresAtUtc,
            user.DisplayName,
            user.TenantId);

        return (response, refreshTokenEntity);
    }
}
