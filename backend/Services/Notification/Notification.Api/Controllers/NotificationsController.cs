using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.BLL.Interfaces;
using Notification.Entities.Contracts;
using YTube.Common.Responses;

namespace Notification.Api.Controllers;

/// <summary>Your own notifications. Everything here needs a login.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    /// <summary>Newest first. Pass nextCursor back as ?cursor= for older ones.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? cursor, CancellationToken ct) =>
        Ok(ApiResponse<NotificationPage>.Ok(await notifications.GetPageAsync(UserId, cursor, ct)));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Ok(ApiResponse<UnreadCountDto>.Ok(new UnreadCountDto(await notifications.CountUnreadAsync(UserId, ct))));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(UserId, id, ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(UserId, ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
