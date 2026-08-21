using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TaskFlow.Application.Dtos;

namespace TaskFlow.IntegrationTests;

[Collection("Integration")]
public class AuthTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task Register_ThenLogin_Succeeds()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Password123!", "Test User", "Test Tenant"), TestContext.Current.CancellationToken);
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"), TestContext.Current.CancellationToken);
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        auth.ShouldNotBeNull();
        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!", "Test User", "Test Tenant"), TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_LocksAccount()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!", "Test User", "Test Tenant"), TestContext.Current.CancellationToken);

        // Lockout kicks in after 5 wrong passwords (Program.cs: Lockout.MaxFailedAccessAttempts).
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!"), TestContext.Current.CancellationToken);
        }

        // The 6th attempt — even with the CORRECT password — must still be rejected: the account
        // is locked, not just "still guessing wrong".
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ReusingAnAlreadyRotatedToken_RevokesAllSessions()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!", "Test User", "Test Tenant"), TestContext.Current.CancellationToken);
        var original = await register.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);

        // First refresh: legitimate rotation — old token is now revoked/replaced.
        var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(original!.RefreshToken), TestContext.Current.CancellationToken);
        firstRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Replaying the ORIGINAL (now-rotated-past) token simulates a stolen token being reused —
        // reuse detection must reject it AND revoke every active session for the account.
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(original.RefreshToken), TestContext.Current.CancellationToken);
        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Proof the whole session tree died: even the token issued by the (legitimate) first
        // refresh above no longer works, because reuse detection revoked it too.
        var secondRefreshData = await firstRefresh.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        var afterReuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(secondRefreshData!.RefreshToken), TestContext.Current.CancellationToken);
        afterReuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_AfterTokenLifetimeElapses_IsRejectedAsExpired_NotAsReuse()
    {
        // Proves the TimeProvider abstraction is actually load-bearing: a token that is simply
        // expired (clock moved past ExpiresAtUtc, never revoked) must fail with a DIFFERENT reason
        // than a reused/revoked token (Refresh_ReusingAnAlreadyRotatedToken_RevokesAllSessions
        // above) — both surface as 401 at the HTTP layer (AuthController.Refresh always returns
        // Unauthorized on failure), but AuthService.RefreshAsync distinguishes "invalid or expired"
        // (IsActive/expiry check, ResultErrorType.NotFound) from "already used, sessions revoked"
        // (reuse detection, ResultErrorType.Conflict) — asserting the message proves the expiry
        // branch fired, not the reuse branch. That distinction was untestable before: without a
        // controllable clock there was no way to move 30 days into the future without waiting it.
        // FakeTimeProvider refuses to move backward (by design — it would falsify anything that
        // already read the clock), so this test only ever advances it forward and never restores
        // real time afterward. That's safe here: no other test in this collection asserts against
        // absolute wall-clock values, only relative deltas and DB state that ResetDatabaseAsync
        // clears — a clock parked in the future doesn't invalidate anything they check.
        await factory.ResetDatabaseAsync();

        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Password123!", "Test User", "Test Tenant"), TestContext.Current.CancellationToken);
        var original = await register.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);

        // One day before expiry (refresh tokens live 30 days, per AuthService.RefreshTokenLifetime)
        // the token must still work — proves the fake clock actually flows through TimeProvider
        // into RefreshToken.IsActive rather than the check being a no-op.
        factory.TimeProvider.Advance(TimeSpan.FromDays(29));
        var stillValid = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(original!.RefreshToken), TestContext.Current.CancellationToken);
        stillValid.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotated = await stillValid.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);

        // Push the newly-rotated token's own clock past its 30-day expiry without ever touching
        // (revoking) it — a pure time-based expiry, distinct from reuse-triggered revocation.
        factory.TimeProvider.Advance(TimeSpan.FromDays(31));
        var expired = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(rotated!.RefreshToken), TestContext.Current.CancellationToken);
        expired.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var expiredBody = await expired.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        expiredBody.ShouldContain("invalid or expired");
        expiredBody.ShouldNotContain("already been used");
    }
}
