namespace ECommerceAI.Data.Entities;

public class AiRecommendationItem
{
    public Guid Id { get; set; }
    public Guid AiCartId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int? Quantity { get; set; }

    public virtual AiGeneratedCart AiCart { get; set; } = null!;
}
