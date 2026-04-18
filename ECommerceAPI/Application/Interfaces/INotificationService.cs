using ECommerceAPI.Application.DTOs.Notifications;

namespace ECommerceAPI.Application.Interfaces;

public interface INotificationService
{
    Task<NotificationListResponseDto> GetNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        bool? isRead = null);

    Task<NotificationActionResultDto> MarkAsReadAsync(Guid userId, Guid notificationId);

    Task<NotificationActionResultDto> MarkAllAsReadAsync(Guid userId);

    /// <summary>
    /// Lưu thông báo trong DB; tùy chọn đưa email vào hàng đợi (gửi bởi background service).
    /// Nếu <paramref name="emailHtmlBody"/> có giá trị, email dùng HTML này thay vì chỉ bọc <paramref name="content"/>.
    /// </summary>
    Task PublishAsync(
        Guid userId,
        string type,
        string title,
        string content,
        string? referenceType = null,
        Guid? referenceId = null,
        bool queueEmail = false,
        string? emailHtmlBody = null,
        string? emailSubjectOverride = null,
        CancellationToken cancellationToken = default);

   
    Task PublishToUsersWithRoleAsync(
        string roleCode,
        string type,
        string title,
        string content,
        string? referenceType = null,
        Guid? referenceId = null,
        bool queueEmail = false,
        CancellationToken cancellationToken = default);
}
