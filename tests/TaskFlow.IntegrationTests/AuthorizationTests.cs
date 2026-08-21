using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TaskFlow.Application.Dtos;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

[Collection("Integration")]
public class AuthorizationTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task Viewer_CannotCreateCard_ButOwnerCan()
    {
        await factory.ResetDatabaseAsync();
        var (owner, _) = await factory.RegisterAndAuthenticateAsync("Viewer Test Tenant");
        var createProject = await owner.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Board Project"), TestContext.Current.CancellationToken);
        var project = await createProject.Content.ReadFromJsonAsync<ProjectDto>(TestContext.Current.CancellationToken);

        // AddProjectMemberRequest only accepts an email that already belongs to the SAME tenant
        // (ProjectService.AddMemberAsync) — the public register endpoint always spins up a brand
        // new tenant, so there is no API-level way to land a second user in an existing tenant.
        // Reassigning TenantId directly here is test-only plumbing to reach that state; every
        // permission check exercised below still runs through the real HTTP pipeline.
        var (viewerClient, viewerEmail) = await factory.RegisterIntoTenantAsync(project!.Id);

        var addMember = await owner.PostAsJsonAsync($"/api/projects/{project.Id}/members",
            new AddProjectMemberRequest(viewerEmail, ProjectRole.Viewer), TestContext.Current.CancellationToken);
        addMember.StatusCode.ShouldBe(HttpStatusCode.OK);

        var board = await owner.GetFromJsonAsync<BoardDto>($"/api/boards/{project.BoardId}", TestContext.Current.CancellationToken);
        var listId = board!.CardLists[0].Id;

        // Real member, real role, real board — Viewer is read-only by policy (ProjectMember
        // requires >= Member), enforced server-side regardless of what the client sends.
        var viewerCreateCard = await viewerClient.PostAsJsonAsync($"/api/boards/{project.BoardId}/cards",
            new CreateCardRequest(listId, "Should not be created", null, CardPriority.Low, null, null), TestContext.Current.CancellationToken);
        viewerCreateCard.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The Owner (added automatically by Project.Create) can.
        var ownerCreateCard = await owner.PostAsJsonAsync($"/api/boards/{project.BoardId}/cards",
            new CreateCardRequest(listId, "Owner-created card", null, CardPriority.Low, null, null), TestContext.Current.CancellationToken);
        ownerCreateCard.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonOwner_CannotDeleteProject()
    {
        await factory.ResetDatabaseAsync();
        var (owner, _) = await factory.RegisterAndAuthenticateAsync("Delete Test Tenant");
        var createProject = await owner.PostAsJsonAsync("/api/projects", new CreateProjectRequest("To Delete"), TestContext.Current.CancellationToken);
        var project = await createProject.Content.ReadFromJsonAsync<ProjectDto>(TestContext.Current.CancellationToken);

        var (stranger, _) = await factory.RegisterAndAuthenticateAsync("Other Tenant");
        var deleteAttempt = await stranger.DeleteAsync($"/api/projects/{project!.Id}", TestContext.Current.CancellationToken);
        deleteAttempt.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
