using System.Net;
using ECommerceAPI.Application.DTOs.Notifications;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Application.Notifications;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationQueue _notificationQueue;

    public NotificationService(ApplicationDbContext context, INotificationQueue notificationQueue)
    {
        _context = context;
        _notificationQueue = notificationQueue;
    }

    public async Task<NotificationListResponseDto> GetNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        bool? isRead = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = _context.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        if (isRead.HasValue)
            baseQuery = baseQuery.Where(n => n.IsRead == isRead.Value);

        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        var totalCount = await baseQuery.CountAsync();

        var items = await baseQuery
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationItemDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Content = n.Content,
                ReferenceType = n.ReferenceType,
                ReferenceId = n.ReferenceId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return new NotificationListResponseDto
        {
            Success = true,
            Notifications = items,
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<NotificationActionResultDto> MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        var affected = await _context.Notifications
            .Where(n => n.Id == notificationId && n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        if (affected == 0)
        {
            var exists = await _context.Notifications.AnyAsync(n => n.Id == notificationId && n.UserId == userId);
            if (!exists)
            {
                return new NotificationActionResultDto
                {
                    Success = false,
                    Message = "Không tìm thấy thông báo",
                    UpdatedCount = 0
                };
            }

            return new NotificationActionResultDto
            {
                Success = true,
                Message = "Thông báo đã được đánh dấu đọc trước đó",
                UpdatedCount = 0
            };
        }

        return new NotificationActionResultDto
        {
            Success = true,
            Message = "Đã đánh dấu đã đọc",
            UpdatedCount = affected
        };
    }

    public async Task<NotificationActionResultDto> MarkAllAsReadAsync(Guid userId)
    {
        var affected = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        return new NotificationActionResultDto
        {
            Success = true,
            Message = affected > 0 ? "Đã đánh dấu tất cả đã đọc" : "Không có thông báo chưa đọc",
            UpdatedCount = affected
        };
    }

    public async Task PublishAsync(
        Guid userId,
        string type,
        string title,
        string content,
        string? referenceType = null,
        Guid? referenceId = null,
        bool queueEmail = false,
        CancellationToken cancellationToken = default)
    {
        var entity = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Content = content,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        if (!queueEmail)
            return;

        var safe = WebUtility.HtmlEncode(content).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br/>", StringComparison.Ordinal);
        var html = $"<html><body style=\"font-family:sans-serif\"><p>{safe}</p></body></html>";
        await _notificationQueue.EnqueueEmailAsync(
            new NotificationEmailJob(userId, title, html),
            cancellationToken);
    }
}
