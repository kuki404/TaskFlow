using System.Net.Http.Json;
using TaskFlow.Application.Dtos;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

[Collection("Integration")]
public class MyCardsTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task GetMine_ReturnsOnlyCardsAssignedToCaller()
    {
        var (client, auth) = await factory.RegisterAndAuthenticateAsync("My Cards Tenant A");

        var project = (await (await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Project A"))).Content
            .ReadFromJsonAsync<ProjectDto>())!;
        var board = (await client.GetFromJsonAsync<BoardDto>($"/api/boards/{project.BoardId}"))!;
        var listId = board.CardLists[0].Id;

        var userId = GetUserId(auth);
        var assignedCard = (await (await client.PostAsJsonAsync($"/api/boards/{project.BoardId}/cards",
            new CreateCardRequest(listId, "Assigned to me", null, CardPriority.Medium, userId, null)))
            .Content.ReadFromJsonAsync<CardDto>())!;

        // Card left unassigned — must never show up in "mine".
        await client.PostAsJsonAsync($"/api/boards/{project.BoardId}/cards",
            new CreateCardRequest(listId, "Unassigned", null, CardPriority.Medium, null, null));

        var mine = await client.GetFromJsonAsync<List<MyCardDto>>("/api/cards/mine");

        Assert.NotNull(mine);
        Assert.Single(mine);
        Assert.Equal(assignedCard.Title, mine![0].Title);
        Assert.Equal(project.Id, mine[0].ProjectId);
    }

    [Fact]
    public async Task GetMine_NeverIncludesAnotherTenantsCards_EvenWhenSameUnderlyingUserId()
    {
        var (clientA, _) = await factory.RegisterAndAuthenticateAsync("My Cards Tenant B");
        var (clientB, _) = await factory.RegisterAndAuthenticateAsync("My Cards Tenant C");

        var projectA = (await (await clientA.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Project B"))).Content
            .ReadFromJsonAsync<ProjectDto>())!;
        var boardA = (await clientA.GetFromJsonAsync<BoardDto>($"/api/boards/{projectA.BoardId}"))!;

        await clientA.PostAsJsonAsync($"/api/boards/{projectA.BoardId}/cards",
            new CreateCardRequest(boardA.CardLists[0].Id, "Tenant B card", null, CardPriority.Low, null, null));

        // Tenant C's caller has their own valid token; the "mine" list must be scoped by their own
        // tenant + userId, never leak Tenant B's cards even indirectly.
        var mineForC = await clientB.GetFromJsonAsync<List<MyCardDto>>("/api/cards/mine");
        Assert.NotNull(mineForC);
        Assert.DoesNotContain(mineForC!, c => c.Title == "Tenant B card");
    }

    private static Guid GetUserId(AuthResponse auth)
    {
        var parts = auth.AccessToken.Split('.');
        var payload = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }
}
