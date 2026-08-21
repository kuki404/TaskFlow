using System.Net.Http.Json;
using Shouldly;
using TaskFlow.Application.Dtos;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

[Collection("Integration")]
public class MyCardsTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task GetMine_ReturnsOnlyCardsAssignedToCaller()
    {
        await factory.ResetDatabaseAsync();
        var (client, auth) = await factory.RegisterAndAuthenticateAsync("My Cards Tenant A");

        var project = (await (await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Project A"), TestContext.Current.CancellationToken)).Content
            .ReadFromJsonAsync<ProjectDto>(TestContext.Current.CancellationToken))!;
        var board = (await client.GetFromJsonAsync<BoardDto>($"/api/boards/{project.BoardId}", TestContext.Current.CancellationToken))!;
        var listId = board.CardLists[0].Id;

        var userId = GetUserId(auth);
        var assignedCard = (await (await client.PostAsJsonAsync($"/api/boards/{project.BoardId}/cards",
            new CreateCardRequest(listId, "Assigned to me", null, CardPriority.Medium, userId, null), TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<CardDto>(TestContext.Current.CancellationToken))!;

        // Card left unassigned — must never show up in "mine".
        await client.PostAsJsonAsync($"/api/boards/{project.BoardId}/cards",
            new CreateCardRequest(listId, "Unassigned", null, CardPriority.Medium, null, null), TestContext.Current.CancellationToken);

        var mine = await client.GetFromJsonAsync<List<MyCardDto>>("/api/cards/mine", TestContext.Current.CancellationToken);

        mine.ShouldNotBeNull();
        mine.Count.ShouldBe(1);
        mine[0].Title.ShouldBe(assignedCard.Title);
        mine[0].ProjectId.ShouldBe(project.Id);
    }

    [Fact]
    public async Task GetMine_NeverIncludesAnotherTenantsCards_EvenWhenSameUnderlyingUserId()
    {
        await factory.ResetDatabaseAsync();
        var (clientA, _) = await factory.RegisterAndAuthenticateAsync("My Cards Tenant B");
        var (clientB, _) = await factory.RegisterAndAuthenticateAsync("My Cards Tenant C");

        var projectA = (await (await clientA.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Project B"), TestContext.Current.CancellationToken)).Content
            .ReadFromJsonAsync<ProjectDto>(TestContext.Current.CancellationToken))!;
        var boardA = (await clientA.GetFromJsonAsync<BoardDto>($"/api/boards/{projectA.BoardId}", TestContext.Current.CancellationToken))!;

        await clientA.PostAsJsonAsync($"/api/boards/{projectA.BoardId}/cards",
            new CreateCardRequest(boardA.CardLists[0].Id, "Tenant B card", null, CardPriority.Low, null, null), TestContext.Current.CancellationToken);

        // Tenant C's caller has their own valid token; the "mine" list must be scoped by their own
        // tenant + userId, never leak Tenant B's cards even indirectly.
        var mineForC = await clientB.GetFromJsonAsync<List<MyCardDto>>("/api/cards/mine", TestContext.Current.CancellationToken);
        mineForC.ShouldNotBeNull();
        mineForC.ShouldNotContain(c => c.Title == "Tenant B card");
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
