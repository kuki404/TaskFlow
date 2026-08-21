using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Application.Common;
using TaskFlow.Application.Dtos;

namespace TaskFlow.Web.Services;

/// <summary>Thin typed HttpClient wrapper around TaskFlow.Api. Attaches the access token to every call and transparently refreshes it once on a 401 before giving up.</summary>
public class TaskFlowApiClient(HttpClient http, AuthSession session)
{
    public async Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/register", request);
        if (!response.IsSuccessStatusCode)
        {
            return (false, await ReadErrorAsync(response));
        }

        await session.SetAsync((await response.Content.ReadFromJsonAsync<AuthResponse>())!);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request);
        if (!response.IsSuccessStatusCode)
        {
            return (false, await ReadErrorAsync(response));
        }

        await session.SetAsync((await response.Content.ReadFromJsonAsync<AuthResponse>())!);
        return (true, null);
    }

    public Task LogoutAsync() => session.ClearAsync();

    public Task<PagedResult<ProjectDto>?> GetProjectsAsync(int page = 1, int pageSize = 50) =>
        SendAsync<PagedResult<ProjectDto>>(HttpMethod.Get, $"api/projects?page={page}&pageSize={pageSize}");

    public Task<ProjectDto?> CreateProjectAsync(CreateProjectRequest request) =>
        SendAsync<ProjectDto>(HttpMethod.Post, "api/projects", request);

    public Task<IReadOnlyList<MyCardDto>?> GetMyCardsAsync() =>
        SendAsync<IReadOnlyList<MyCardDto>>(HttpMethod.Get, "api/cards/mine");

    public Task<IReadOnlyList<ProjectMemberDto>?> GetMembersAsync(Guid projectId) =>
        SendAsync<IReadOnlyList<ProjectMemberDto>>(HttpMethod.Get, $"api/projects/{projectId}/members");

    public Task<BoardDto?> GetBoardAsync(Guid boardId) =>
        SendAsync<BoardDto>(HttpMethod.Get, $"api/boards/{boardId}");

    public Task<CardListDto?> CreateListAsync(Guid boardId, CreateCardListRequest request) =>
        SendAsync<CardListDto>(HttpMethod.Post, $"api/boards/{boardId}/lists", request);

    public Task<CardDto?> CreateCardAsync(Guid boardId, CreateCardRequest request) =>
        SendAsync<CardDto>(HttpMethod.Post, $"api/boards/{boardId}/cards", request);

    public async Task<(CardDto? Card, string? Error)> MoveCardAsync(Guid boardId, Guid cardId, MoveCardRequest request)
    {
        // Regression fix: this sent POST while BoardsController declares [HttpPut(".../move")],
        // so every drag-and-drop drop returned 405 and was misreported to the user as a
        // concurrency conflict. Proven live: POST -> 405, PUT -> 200 with the card actually moved.
        var response = await SendRawAsync(HttpMethod.Put, $"api/boards/{boardId}/cards/{cardId}/move", request);
        if (!response.IsSuccessStatusCode)
        {
            return (null, await ReadErrorAsync(response));
        }

        return (await response.Content.ReadFromJsonAsync<CardDto>(), null);
    }

    public async Task<(CardDto? Card, string? Error)> UpdateCardAsync(Guid boardId, Guid cardId, UpdateCardRequest request)
    {
        var response = await SendRawAsync(HttpMethod.Put, $"api/boards/{boardId}/cards/{cardId}", request);
        if (!response.IsSuccessStatusCode)
        {
            return (null, await ReadErrorAsync(response));
        }

        return (await response.Content.ReadFromJsonAsync<CardDto>(), null);
    }

    public Task<HttpResponseMessage> DeleteCardAsync(Guid boardId, Guid cardId) =>
        SendRawAsync(HttpMethod.Delete, $"api/boards/{boardId}/cards/{cardId}");

    public Task<HttpResponseMessage> DeleteListAsync(Guid boardId, Guid cardListId) =>
        SendRawAsync(HttpMethod.Delete, $"api/boards/{boardId}/lists/{cardListId}");

    public Task<(ProjectMemberDto? Member, string? Error)> AddMemberAsync(Guid projectId, AddProjectMemberRequest request) =>
        SendWithErrorAsync<ProjectMemberDto>(HttpMethod.Post, $"api/projects/{projectId}/members", request);

    public Task<HttpResponseMessage> UpdateMemberRoleAsync(Guid projectId, Guid memberId, UpdateProjectMemberRoleRequest request) =>
        SendRawAsync(HttpMethod.Put, $"api/projects/{projectId}/members/{memberId}", request);

    public Task<HttpResponseMessage> RemoveMemberAsync(Guid projectId, Guid memberId) =>
        SendRawAsync(HttpMethod.Delete, $"api/projects/{projectId}/members/{memberId}");

    /// <summary>Decodes the "sub" claim from the current access token — the API never exposes a
    /// "/me" endpoint, and this is the only source of the signed-in user's own UserId on the
    /// client, needed to tell which row in a members list is "you" and what your own role is.</summary>
    public Guid? CurrentUserId
    {
        get
        {
            var token = session.Current?.AccessToken;
            if (token is null)
            {
                return null;
            }

            var parts = token.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            try
            {
                var payload = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=')
                    .Replace('-', '+').Replace('_', '/');
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("sub", out var sub) && Guid.TryParse(sub.GetString(), out var id)
                    ? id
                    : null;
            }
            catch
            {
                return null;
            }
        }
    }

    public string? CurrentAccessToken => session.Current?.AccessToken;

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        var response = await SendRawAsync(method, url, body);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : default;
    }

    private async Task<(T? Result, string? Error)> SendWithErrorAsync<T>(HttpMethod method, string url, object? body = null)
    {
        var response = await SendRawAsync(method, url, body);
        if (!response.IsSuccessStatusCode)
        {
            return (default, await ReadErrorAsync(response));
        }

        return (await response.Content.ReadFromJsonAsync<T>(), null);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, object? body = null)
    {
        var response = await SendOnceAsync(method, url, body);

        if (response.StatusCode == HttpStatusCode.Unauthorized && session.Current is not null)
        {
            var refreshed = await TryRefreshAsync();
            if (refreshed)
            {
                response = await SendOnceAsync(method, url, body);
            }
        }

        return response;
    }

    private Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (session.Current is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Current.AccessToken);
        }

        return http.SendAsync(request);
    }

    private async Task<bool> TryRefreshAsync()
    {
        if (session.Current is null)
        {
            return false;
        }

        var response = await http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(session.Current.RefreshToken));
        if (!response.IsSuccessStatusCode)
        {
            await session.ClearAsync();
            return false;
        }

        await session.SetAsync((await response.Content.ReadFromJsonAsync<AuthResponse>())!);
        return true;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            if (problem is not null && problem.TryGetValue("title", out var error))
            {
                return error.ToString() ?? "Request failed.";
            }
        }
        catch
        {
            // Fall through — the body wasn't JSON we recognize.
        }

        return $"Request failed with status {(int)response.StatusCode}.";
    }
}
