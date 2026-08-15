using System.Net;
using System.Net.Http.Json;
using TaskFlow.Application.Dtos;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

[Collection("Integration")]
public class RbacAndConcurrencyTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task ConcurrentCardUpdates_OneSucceedsAndOneReturnsConflict()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync("Concurrency Co");
        var createProject = await owner.PostAsJsonAsync("/api/projects", new CreateProjectRequest($"Project {Guid.NewGuid():N}"));
        var project = await createProject.Content.ReadFromJsonAsync<ProjectDto>();

        var board = await owner.GetFromJsonAsync<BoardDto>($"/api/boards/{project!.BoardId}");
        var firstList = board!.CardLists[0];

        var createResponse = await owner.PostAsJsonAsync($"/api/boards/{project.BoardId}/cards",
            new CreateCardRequest(firstList.Id, "Race me", null, CardPriority.Low, null, null));
        var card = await createResponse.Content.ReadFromJsonAsync<CardDto>();

        // Both requests read the same RowVersion (captured once, above) and race to update — one
        // must win, the other must get a 409 Conflict from DbUpdateConcurrencyException.
        var update1 = owner.PutAsJsonAsync($"/api/boards/{project.BoardId}/cards/{card!.Id}",
            new UpdateCardRequest("Title A", null, CardPriority.Low, null, null, card.RowVersion));
        var update2 = owner.PutAsJsonAsync($"/api/boards/{project.BoardId}/cards/{card.Id}",
            new UpdateCardRequest("Title B", null, CardPriority.Low, null, null, card.RowVersion));

        var results = await Task.WhenAll(update1, update2);

        Assert.Contains(results, r => r.IsSuccessStatusCode);
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.Conflict);
    }
}
