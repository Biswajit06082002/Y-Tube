using System.Net.Http.Json;
using Feed.BLL.Interfaces;
using YTube.Common.Responses;

namespace Feed.DAL.Clients;

/// <summary>Asks the User service who the current user follows (the user's token is forwarded).</summary>
public sealed class UserServiceClient(HttpClient http) : IUserFollowsClient
{
    public async Task<IReadOnlyList<Guid>> GetFollowingIdsAsync(CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiResponse<IReadOnlyList<Guid>>>("api/users/me/following/ids", ct);
        return response?.Data ?? [];
    }
}
