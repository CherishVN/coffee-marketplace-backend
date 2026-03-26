using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Controllers;

/// <summary>
/// Đọc lịch sử phiên AI chat của người dùng từ DB (ECommerceAPI).
/// Python AI service ghi dữ liệu vào cùng DB, controller này chỉ đọc.
/// </summary>
[ApiController]
[Route("api/ai/sessions")]
[Authorize]
public class AiSessionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IUserClaimsService _userClaims;

    public AiSessionsController(ApplicationDbContext context, IUserClaimsService userClaims)
    {
        _context = context;
        _userClaims = userClaims;
    }

    /// <summary>
    /// Lấy danh sách các phiên chat AI của user, sắp xếp theo mới nhất.
    /// Title = nội dung tin nhắn user đầu tiên (truncated).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSessions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = _userClaims.GetUserId();
        if (userId == null) return Unauthorized();

        var sessions = await _context.AiChatSessions
            .Where(s => s.UserId == userId.Value)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                sessionId   = s.Id,
                status      = s.Status,
                createdAt   = s.CreatedAt,
                updatedAt   = s.UpdatedAt,
                title       = s.AiChatMessages
                    .Where(m => m.Role == "user")
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => m.Content.Length > 80 ? m.Content.Substring(0, 80) + "…" : m.Content)
                    .FirstOrDefault() ?? "Cuộc trò chuyện mới",
                lastMessage = s.AiChatMessages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new { m.Role, content = m.Content.Length > 60 ? m.Content.Substring(0, 60) + "…" : m.Content, m.CreatedAt })
                    .FirstOrDefault(),
                messageCount = s.AiChatMessages.Count,
            })
            .ToListAsync();

        var totalCount = await _context.AiChatSessions
            .CountAsync(s => s.UserId == userId.Value);

        return Ok(new { success = true, sessions, totalCount, page, pageSize });
    }

    /// <summary>
    /// Lấy toàn bộ tin nhắn của một phiên chat cụ thể.
    /// </summary>
    [HttpGet("{sessionId:guid}/messages")]
    public async Task<IActionResult> GetSessionMessages(Guid sessionId)
    {
        var userId = _userClaims.GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _context.AiChatSessions
            .Where(s => s.Id == sessionId && s.UserId == userId.Value)
            .FirstOrDefaultAsync();

        if (session == null) return NotFound(new { success = false, message = "Không tìm thấy phiên chat" });

        var messages = await _context.AiChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                id        = m.Id.ToString(),
                role      = m.Role,
                content   = m.Content,
                createdAt = m.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { success = true, sessionId, messages });
    }
}
