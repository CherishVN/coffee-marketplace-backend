using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IUserClaimsService _userClaimsService;

    public NotificationsController(
        INotificationService notificationService,
        IUserClaimsService userClaimsService)
    {
        _notificationService = notificationService;
        _userClaimsService = userClaimsService;
    }

    /// <summary>
    /// Danh sách thông báo của user đăng nhập (phân trang, lọc theo đã đọc/chưa đọc).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _notificationService.GetNotificationsAsync(userId.Value, page, pageSize, isRead);
        return Ok(result);
    }

    /// <summary>
    /// Đánh dấu một thông báo đã đọc.
    /// </summary>
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _notificationService.MarkAsReadAsync(userId.Value, id);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Đánh dấu tất cả thông báo đã đọc.
    /// </summary>
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _notificationService.MarkAllAsReadAsync(userId.Value);
        return Ok(result);
    }
}
