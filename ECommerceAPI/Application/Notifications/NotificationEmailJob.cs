namespace ECommerceAPI.Application.Notifications;

/// <summary>
/// Việc gửi email thông báo được đưa vào hàng đợi để xử lý nền.
/// </summary>
public sealed record NotificationEmailJob(Guid TargetUserId, string Subject, string HtmlBody);
