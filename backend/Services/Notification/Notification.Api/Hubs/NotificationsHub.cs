using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Notification.Api.Hubs;

/// <summary>
/// Real-time channel to the browser. The client only listens: the server calls
/// "notification" on a user's connections when something happens. A user can have several
/// connections (tabs, phone + laptop); Clients.User(id) reaches all of them.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";
    public const string NotificationMethod = "notification";
}

/// <summary>SignalR identifies users by this value. Our JWTs carry the user id in "sub".</summary>
public sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
