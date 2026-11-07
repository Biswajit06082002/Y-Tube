using System.Net.Http.Json;
using Comment.BLL.Interfaces;
using Comment.Entities.Contracts;
using YTube.Common.Responses;

namespace Comment.DAL.Clients;

/// <summary>Asks the User service who is writing (token forwarded) and who the @handles belong to.</summary>
public sealed class UserServiceClient(HttpClient http) : IUserClient
{
    public async Task<AuthorInfo?> GetMeAsync(CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiResponse<UserResponse>>("api/users/me", ct);
        return response?.Data is { } u ? new AuthorInfo(u.UserId, u.DisplayName, u.Handle) : null;
    }

    public async Task<IReadOnlyList<AuthorInfo>> GetByHandlesAsync(IReadOnlyCollection<string> handles, CancellationToken ct = default)
    {
        var query = string.Join('&', handles.Select(h => $"handle={Uri.EscapeDataString(h)}"));
        var response = await http.GetFromJsonAsync<ApiResponse<List<UserResponse>>>($"api/users/by-handles?{query}", ct);
        return [.. (response?.Data ?? []).Select(u => new AuthorInfo(u.UserId, u.DisplayName, u.Handle))];
    }

    private sealed record UserResponse(Guid UserId, string Handle, string DisplayName);
}
