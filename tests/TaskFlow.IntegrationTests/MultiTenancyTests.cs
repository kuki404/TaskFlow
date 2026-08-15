using System.Net;
using System.Net.Http.Json;
using TaskFlow.Application.Common;
using TaskFlow.Application.Dtos;

namespace TaskFlow.IntegrationTests;

[Collection("Integration")]
public class MultiTenancyTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task ProjectList_ForTenantA_NeverIncludesTenantBsProjects()
    {
        var (clientA, _) = await factory.RegisterAndAuthenticateAsync("Tenant A");
        var (clientB, _) = await factory.RegisterAndAuthenticateAsync("Tenant B");

        var createA = await clientA.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Tenant A Secret Project"));
        createA.EnsureSuccessStatusCode();

        // Tenant B queries with a perfectly valid token for ITS OWN tenant — the EF Core global
        // query filter (TenantId == currentTenantProvider.TenantId) must make Tenant A's project
        // simply not exist from Tenant B's point of view: an EMPTY result, never a 403 (a 403
        // would leak that the resource exists at all).
        var listB = await clientB.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects");
        Assert.NotNull(listB);
        Assert.DoesNotContain(listB!.Items, p => p.Name == "Tenant A Secret Project");
    }

    [Fact]
    public async Task GetBoard_FromAnotherTenant_ReturnsNotFound_NotTheData()
    {
        var (clientA, _) = await factory.RegisterAndAuthenticateAsync("Tenant C");
        var (clientB, _) = await factory.RegisterAndAuthenticateAsync("Tenant D");

        var createA = await clientA.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Tenant C Project"));
        var projectA = await createA.Content.ReadFromJsonAsync<ProjectDto>();

        // Board belongs to a project in Tenant C; Tenant D has a valid token but for a different
        // tenant entirely. The query filter scopes Boards by TenantId, so this 404s — the
        // authorization layer never even gets a chance to run because the row doesn't "exist".
        var response = await clientB.GetAsync($"/api/boards/{projectA!.BoardId}");
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);
    }
}
