namespace ECommerceAI.Data.Entities;

public class AiChatSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = "active";
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AiChatMessage> Messages { get; set; } = new List<AiChatMessage>();
    public virtual ICollection<AiGeneratedCart> GeneratedCarts { get; set; } = new List<AiGeneratedCart>();
    public virtual ICollection<AiProductRecommendation> Recommendations { get; set; } = new List<AiProductRecommendation>();
    public virtual AiChatSessionPreference? Preference { get; set; }
}
