using ECommerceAPI.Application.DTOs.Chat;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationController : ControllerBase
{
    private readonly IConversationService _conversationService;
    private readonly IUserClaimsService _userClaimsService;

    public ConversationController(IConversationService conversationService, IUserClaimsService userClaimsService)
    {
        _conversationService = conversationService;
        _userClaimsService = userClaimsService;
    }

    /// <summary>Tạo mới hoặc lấy conversation với shop (buyer dùng)</summary>
    [HttpPost]
    public async Task<IActionResult> StartOrGetConversation([FromBody] StartConversationDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _conversationService.StartOrGetConversationAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>Lấy danh sách tất cả conversations của tôi</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyConversations()
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _conversationService.GetMyConversationsAsync(userId.Value);

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>Lấy lịch sử tin nhắn trong conversation (phân trang, tin mới nhất trước)</summary>
    [HttpGet("{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _conversationService.GetConversationMessagesAsync(userId.Value, conversationId, page, pageSize);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>Gửi tin nhắn trong conversation</summary>
    [HttpPost("{conversationId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid conversationId,
        [FromBody] SendMessageDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _conversationService.SendMessageAsync(userId.Value, conversationId, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>Đánh dấu tất cả tin nhắn trong conversation là đã đọc</summary>
    [HttpPut("{conversationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _conversationService.MarkAsReadAsync(userId.Value, conversationId);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }
}
