using ECommerceAI.DTOs.Chat;
using ECommerceAI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECommerceAI.Controllers;

[ApiController]
[Route("api/ai/chat")]
[Authorize]
public class AiChatController : ControllerBase
{
    private readonly IAiChatService _chatService;

    public AiChatController(IAiChatService chatService)
    {
        _chatService = chatService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Không xác định được user"));

    /// <summary>Tạo hoặc lấy session chat hiện tại</summary>
    [HttpPost("session")]
    public async Task<IActionResult> GetOrCreateSession()
    {
        var userId = GetUserId();
        var result = await _chatService.GetOrCreateSessionAsync(userId);
        return Ok(result);
    }

    /// <summary>Gửi tin nhắn và nhận phản hồi từ AI</summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequestDto dto)
    {
        var userId = GetUserId();
        var result = await _chatService.SendMessageAsync(dto.SessionId, userId, dto.Message);
        return Ok(result);
    }

    /// <summary>Xác nhận tạo đơn hàng sau khi AI hỏi</summary>
    [HttpPost("confirm-order")]
    public async Task<IActionResult> ConfirmOrder([FromBody] ConfirmOrderRequestDto dto)
    {
        var userId = GetUserId();
        var result = await _chatService.ConfirmOrderAsync(dto.SessionId, userId, dto.CartId, dto.ShippingAddressId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>Lấy lịch sử chat của session</summary>
    [HttpGet("history/{sessionId:guid}")]
    public async Task<IActionResult> GetHistory(Guid sessionId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _chatService.GetHistoryAsync(sessionId, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Không tìm thấy session chat" });
        }
    }
}
