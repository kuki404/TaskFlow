using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Application.Dtos;
using TaskFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.IntegrationTests;

/// <summary>Shared helpers so every test doesn't hand-roll "register a user and get an authenticated HttpClient".</summary>
public static class TestClientExtensions
{
    public static async Task<(HttpClient Client, AuthResponse Auth)> RegisterAndAuthenticateAsync(
        this TaskFlowWebApplicationFactory factory, string? tenantName = null)
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Password123!", "Test User", tenantName ?? $"Tenant-{Guid.NewGuid():N}"), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    /// <summary>
    /// Registers a brand-new user (which always creates its own tenant, by design — there is no
    /// public "join an existing tenant" endpoint) and then reassigns it into the SAME tenant that
    /// owns projectId, purely so RBAC tests can reach "two distinct users, one tenant" without a
    /// product feature that doesn't exist yet. The DB write is test-only plumbing; every
    /// permission check exercised against the returned client still runs through the real HTTP
    /// pipeline with a freshly issued (re-logged-in) token carrying the correct tenant_id claim.
    /// </summary>
    public static async Task<(HttpClient Client, string Email)> RegisterIntoTenantAsync(
        this TaskFlowWebApplicationFactory factory, Guid projectId, string password = "Password123!")
    {
        var email = $"user-{Guid.NewGuid():N}@test.local";
        var bootstrapClient = factory.CreateClient();
        var register = await bootstrapClient.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Password123!", "Test Viewer", $"Throwaway-{Guid.NewGuid():N}"), TestContext.Current.CancellationToken);
        register.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
            // No HttpContext exists in this scope, so the tenant query filter has nothing to
            // compare against — IgnoreQueryFilters bypasses it for this test-only lookup.
            var targetTenantId = await db.Projects.IgnoreQueryFilters().Where(p => p.Id == projectId).Select(p => p.TenantId).FirstAsync(TestContext.Current.CancellationToken);
            var user = await db.Users.FirstAsync(u => u.Email == email, TestContext.Current.CancellationToken);
            user.TenantId = targetTenantId;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"), TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return (client, email);
    }
}
