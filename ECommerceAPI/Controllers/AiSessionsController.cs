using System.Text.Json;
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
    public sealed class ToggleMuteRequest
    {
        public bool IsMuted { get; set; }
    }

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
            .Where(s => s.UserId == userId.Value && (s.Preference == null || !s.Preference.IsDeleted))
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                sessionId   = s.Id,
                status      = s.Status,
                createdAt   = s.CreatedAt,
                updatedAt   = s.UpdatedAt,
                isMuted     = s.Preference != null && s.Preference.IsMuted,
                lastReadMessageId = s.Preference != null ? s.Preference.LastReadMessageId : null,
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

        var sessionIds = sessions.Select(s => (Guid)s.sessionId).ToList();
        var assistantMessages = await _context.AiChatMessages
            .Where(m => sessionIds.Contains(m.SessionId) && m.Role == "assistant")
            .Select(m => new { m.SessionId, m.Id })
            .ToListAsync();

        var sessionItems = sessions.Select(s =>
        {
            var sid = (Guid)s.sessionId;
            var lastRead = (long?)s.lastReadMessageId;
            var unreadCount = assistantMessages
                .Where(m => m.SessionId == sid)
                .Count(m => !lastRead.HasValue || m.Id > lastRead.Value);

            return new
            {
                s.sessionId,
                s.status,
                s.createdAt,
                s.updatedAt,
                s.title,
                s.lastMessage,
                s.messageCount,
                s.isMuted,
                unreadCount,
            };
        }).ToList();

        var totalCount = await _context.AiChatSessions
            .CountAsync(s => s.UserId == userId.Value && (s.Preference == null || !s.Preference.IsDeleted));

        return Ok(new { success = true, sessions = sessionItems, totalCount, page, pageSize });
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

        var rows = await _context.AiChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Role,
                m.Content,
                m.CreatedAt,
                m.SuggestedProductsJson,
            })
            .ToListAsync();

        var messages = rows.Select(m => new
        {
            id = m.Id.ToString(),
            role      = m.Role,
            content   = m.Content,
            createdAt = m.CreatedAt,
            products  = ParseSuggestedProductsJson(m.SuggestedProductsJson),
        }).ToList();

        return Ok(new { success = true, sessionId, messages });
    }

    [HttpPost("{sessionId:guid}/read")]
    public async Task<IActionResult> MarkSessionAsRead(Guid sessionId)
    {
        var userId = _userClaims.GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _context.AiChatSessions
            .Where(s => s.Id == sessionId && s.UserId == userId.Value)
            .FirstOrDefaultAsync();

        if (session == null) return NotFound(new { success = false, message = "Không tìm thấy phiên chat" });

        var latestMessageId = await _context.AiChatMessages
            .Where(m => m.SessionId == sessionId)
            .MaxAsync(m => (long?)m.Id);

        var pref = await _context.AiChatSessionPreferences
            .FirstOrDefaultAsync(p => p.SessionId == sessionId);

        if (pref == null)
        {
            pref = new Domain.Entities.AiChatSessionPreference
            {
                SessionId = sessionId,
                IsMuted = false,
                IsDeleted = false,
            };
            _context.AiChatSessionPreferences.Add(pref);
        }

        pref.LastReadMessageId = latestMessageId;
        pref.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { success = true, sessionId, unreadCount = 0 });
    }

    [HttpPatch("{sessionId:guid}/mute")]
    public async Task<IActionResult> ToggleMute(Guid sessionId, [FromBody] ToggleMuteRequest request)
    {
        var userId = _userClaims.GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _context.AiChatSessions
            .Where(s => s.Id == sessionId && s.UserId == userId.Value)
            .FirstOrDefaultAsync();

        if (session == null) return NotFound(new { success = false, message = "Không tìm thấy phiên chat" });

        var pref = await _context.AiChatSessionPreferences
            .FirstOrDefaultAsync(p => p.SessionId == sessionId);

        if (pref == null)
        {
            pref = new Domain.Entities.AiChatSessionPreference
            {
                SessionId = sessionId,
                LastReadMessageId = null,
                IsDeleted = false,
            };
            _context.AiChatSessionPreferences.Add(pref);
        }

        pref.IsMuted = request.IsMuted;
        pref.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { success = true, sessionId, isMuted = pref.IsMuted });
    }

    [HttpDelete("{sessionId:guid}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId)
    {
        var userId = _userClaims.GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _context.AiChatSessions
            .Where(s => s.Id == sessionId && s.UserId == userId.Value)
            .FirstOrDefaultAsync();

        if (session == null) return NotFound(new { success = false, message = "Không tìm thấy phiên chat" });

        var pref = await _context.AiChatSessionPreferences
            .FirstOrDefaultAsync(p => p.SessionId == sessionId);

        if (pref == null)
        {
            pref = new Domain.Entities.AiChatSessionPreference
            {
                SessionId = sessionId,
                IsMuted = false,
                LastReadMessageId = null,
            };
            _context.AiChatSessionPreferences.Add(pref);
        }

        pref.IsDeleted = true;
        pref.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { success = true, sessionId });
    }

    private static readonly JsonSerializerOptions SuggestedProductJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static List<AiChatHistoryProductItem>? ParseSuggestedProductsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<AiChatHistoryProductItem>>(json, SuggestedProductJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private sealed class AiChatHistoryProductItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public decimal BasePrice { get; set; }
        public string? ImageUrl { get; set; }
        public string? CategoryName { get; set; }
        public string? Slug { get; set; }
        public decimal? MatchScore { get; set; }
        public string? MatchReason { get; set; }
    }
}
