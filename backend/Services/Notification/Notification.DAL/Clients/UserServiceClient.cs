using System.Net.Http.Json;
using Notification.BLL.Interfaces;
using YTube.Common.Responses;

namespace Notification.DAL.Clients;

/// <summary>Asks the User service who follows a creator (for new-video notifications).</summary>
public sealed class UserServiceClient(HttpClient http) : IFollowersClient
{
    public async Task<IReadOnlyList<Guid>> GetFollowerIdsAsync(Guid creatorId, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiResponse<List<Guid>>>($"api/users/{creatorId}/followers/ids", ct);
        return response?.Data ?? [];
    }
}
