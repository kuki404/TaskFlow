using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TaskFlow.Application.Common;
using TaskFlow.Application.Dtos;

namespace TaskFlow.IntegrationTests;

[Collection("Integration")]
public class PaginationTests(TaskFlowWebApplicationFactory factory)
{
    [Fact]
    public async Task Pagination_PageSizeIsCappedServerSide()
    {
        await factory.ResetDatabaseAsync();
        var (client, _) = await factory.RegisterAndAuthenticateAsync("Pagination Tenant");

        // PagedRequest's [Range(1, 100)] rejects a client-requested page size above the server
        // cap outright (model validation), rather than silently clamping it — the client finds
        // out its request was invalid instead of getting a surprising, smaller-than-asked page.
        var response = await client.GetAsync("/api/projects?page=1&pageSize=99999", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var validResponse = await client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?page=1&pageSize=100", TestContext.Current.CancellationToken);
        validResponse.ShouldNotBeNull();
        validResponse.PageSize.ShouldBeLessThanOrEqualTo(PagedRequest.MaxPageSize);
    }
}
