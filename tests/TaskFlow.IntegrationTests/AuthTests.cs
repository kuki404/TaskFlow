using System.Net;
using System.Net.Http.Json;
using TaskFlow.Application.Dtos;

namespace TaskFlow.IntegrationTests;

[Collection("Integration")]
public class AuthTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task Register_ThenLogin_Succeeds()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Password123!", "Test User", "Test Tenant"));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!", "Test User", "Test Tenant"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_LocksAccount()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!", "Test User", "Test Tenant"));

        // Lockout kicks in after 5 wrong passwords (Program.cs: Lockout.MaxFailedAccessAttempts).
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!"));
        }

        // The 6th attempt — even with the CORRECT password — must still be rejected: the account
        // is locked, not just "still guessing wrong".
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReusingAnAlreadyRotatedToken_RevokesAllSessions()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!", "Test User", "Test Tenant"));
        var original = await register.Content.ReadFromJsonAsync<AuthResponse>();

        // First refresh: legitimate rotation — old token is now revoked/replaced.
        var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(original!.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        // Replaying the ORIGINAL (now-rotated-past) token simulates a stolen token being reused —
        // reuse detection must reject it AND revoke every active session for the account.
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // Proof the whole session tree died: even the token issued by the (legitimate) first
        // refresh above no longer works, because reuse detection revoked it too.
        var secondRefreshData = await firstRefresh.Content.ReadFromJsonAsync<AuthResponse>();
        var afterReuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(secondRefreshData!.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }
}
