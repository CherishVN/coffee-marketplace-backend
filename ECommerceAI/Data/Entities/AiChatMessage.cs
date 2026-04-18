namespace ECommerceAI.Data.Entities;

public class AiChatMessage
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = null!;   // "user" | "assistant" | "system"
    public string Content { get; set; } = null!;
    public string? SuggestedProductsJson { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual AiChatSession Session { get; set; } = null!;
}
