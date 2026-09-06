namespace ECommerceAI.Data.Entities;

public class AiGeneratedCart
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid? CartId { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual AiChatSession Session { get; set; } = null!;
    public virtual ICollection<AiRecommendationItem> Items { get; set; } = new List<AiRecommendationItem>();
}
