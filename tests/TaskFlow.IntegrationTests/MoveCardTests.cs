using System.Net;
using System.Net.Http.Json;
using TaskFlow.Application.Dtos;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Regression coverage for a real bug found by manual testing, not by the existing suite:
/// TaskFlowApiClient.MoveCardAsync sent POST to .../cards/{cardId}/move while BoardsController
/// declares [HttpPut(".../move")] — every drag-and-drop drop returned 405 and was misreported to
/// the user as a concurrency conflict. No test exercised the move endpoint at all before this
/// file, which is exactly why the mismatch shipped.
/// </summary>
[Collection("Integration")]
public class MoveCardTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task MoveCard_ViaPost_ReturnsMethodNotAllowed()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync("Move Verb Co");
        var createProject = await owner.PostAsJsonAsync("/api/projects", new CreateProjectRequest($"Project {Guid.NewGuid():N}"));
        var project = await createProject.Content.ReadFromJsonAsync<ProjectDto>();
        var board = await owner.GetFromJsonAsync<BoardDto>($"/api/boards/{project!.BoardId}");
        var sourceList = board!.CardLists[0];
        var targetList = board.CardLists[1];

        var createCard = await owner.PostAsJsonAsync($"/api/boards/{project.BoardId}/cards",
            new CreateCardRequest(sourceList.Id, "Verb check", null, CardPriority.Low, null, null));
        var card = await createCard.Content.ReadFromJsonAsync<CardDto>();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/boards/{project.BoardId}/cards/{card!.Id}/move")
        {
            Content = JsonContent.Create(new MoveCardRequest(targetList.Id, 0, card.RowVersion))
        };
        var response = await owner.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    /// <summary>The actual fix, proven the same way the bug was: exercising the exact route and verb TaskFlowApiClient.MoveCardAsync now sends.</summary>
    [Fact]
    public async Task MoveCard_ViaPut_MovesTheCardToTheTargetList()
    {
        var (owner, _) = await factory.RegisterAndAuthenticateAsync("Move Fix Co");
        var createProject = await owner.PostAsJsonAsync("/api/projects", new CreateProjectRequest($"Project {Guid.NewGuid():N}"));
        var project = await createProject.Content.ReadFromJsonAsync<ProjectDto>();
        var board = await owner.GetFromJsonAsync<BoardDto>($"/api/boards/{project!.BoardId}");
        var sourceList = board!.CardLists[0];
        var targetList = board.CardLists[1];

        var createCard = await owner.PostAsJsonAsync($"/api/boards/{project.BoardId}/cards",
            new CreateCardRequest(sourceList.Id, "Move me", null, CardPriority.Low, null, null));
        var card = await createCard.Content.ReadFromJsonAsync<CardDto>();

        var response = await owner.PutAsJsonAsync(
            $"/api/boards/{project.BoardId}/cards/{card!.Id}/move", new MoveCardRequest(targetList.Id, 0, card.RowVersion));

        Assert.True(response.IsSuccessStatusCode);
        var moved = await response.Content.ReadFromJsonAsync<CardDto>();
        Assert.Equal(targetList.Id, moved!.CardListId);
    }
}
