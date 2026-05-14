namespace ECommerceAI.Data.Entities;

public class AiChatSessionPreference
{
    public Guid SessionId { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeleted { get; set; }
    public long? LastReadMessageId { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual AiChatSession Session { get; set; } = null!;
}
