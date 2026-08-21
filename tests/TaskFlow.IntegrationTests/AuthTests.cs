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
}
