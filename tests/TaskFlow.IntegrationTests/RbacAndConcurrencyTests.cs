using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TaskFlow.Application.Dtos;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

[Collection("Integration")]
public class RbacAndConcurrencyTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task ConcurrentCardUpdates_OneSucceedsAndOneReturnsConflict()
    {
        await factory.ResetDatabaseAsync();
        var (owner, _) = await factory.RegisterAndAuthenticateAsync("Concurrency Co");
        var createProject = await owner.PostAsJsonAsync("/api/projects", new CreateProjectRequest($"Project {Guid.NewGuid():N}"), TestContext.Current.CancellationToken);
        var project = await createProject.Content.ReadFromJsonAsync<ProjectDto>(TestContext.Current.CancellationToken);

        var board = await owner.GetFromJsonAsync<BoardDto>($"/api/boards/{project!.BoardId}", TestContext.Current.CancellationToken);
        var firstList = board!.CardLists[0];

        var createResponse = await owner.PostAsJsonAsync($"/api/boards/{project.BoardId}/cards",
            new CreateCardRequest(firstList.Id, "Race me", null, CardPriority.Low, null, null), TestContext.Current.CancellationToken);
        var card = await createResponse.Content.ReadFromJsonAsync<CardDto>(TestContext.Current.CancellationToken);

        // Both requests read the same RowVersion (captured once, above) and race to update — one
        // must win, the other must get a 409 Conflict from DbUpdateConcurrencyException.
        var update1 = owner.PutAsJsonAsync($"/api/boards/{project.BoardId}/cards/{card!.Id}",
            new UpdateCardRequest("Title A", null, CardPriority.Low, null, null, card.RowVersion), TestContext.Current.CancellationToken);
        var update2 = owner.PutAsJsonAsync($"/api/boards/{project.BoardId}/cards/{card.Id}",
            new UpdateCardRequest("Title B", null, CardPriority.Low, null, null, card.RowVersion), TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(update1, update2);

        results.ShouldContain(r => r.IsSuccessStatusCode);
        results.ShouldContain(r => r.StatusCode == HttpStatusCode.Conflict);
    }
}
