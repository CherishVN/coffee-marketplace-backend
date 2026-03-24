namespace ECommerceAPI.Application.DTOs.Notifications;

public class NotificationItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<NotificationItemDto> Notifications { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class NotificationActionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int UpdatedCount { get; set; }
}
