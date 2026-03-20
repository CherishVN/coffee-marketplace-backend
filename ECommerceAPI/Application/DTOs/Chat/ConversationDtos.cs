namespace ECommerceAPI.Application.DTOs.Chat;

// ─── Request DTOs ───────────────────────────────────────────────────────────

public class StartConversationDto
{
    public Guid ShopId { get; set; }
    public Guid? OrderId { get; set; }
    public string? FirstMessage { get; set; }
}

public class SendMessageDto
{
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text"; // text | image
}

// ─── Response DTOs ──────────────────────────────────────────────────────────

public class ConversationDto
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string? ShopLogoUrl { get; set; }
    public Guid BuyerId { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public Guid SellerId { get; set; }
    public Guid? OrderId { get; set; }
    public MessageDto? LastMessage { get; set; }
    public int UnreadCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty; // buyer | seller
    public string MessageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ConversationDetailDto
{
    public ConversationDto Conversation { get; set; } = null!;
    public List<MessageDto> Messages { get; set; } = new();
    public int TotalMessages { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
