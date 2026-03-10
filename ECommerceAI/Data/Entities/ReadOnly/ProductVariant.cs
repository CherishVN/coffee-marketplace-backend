using System.Text.Json;

namespace ECommerceAI.Data.Entities.ReadOnly;

public class ProductVariant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string? Sku { get; set; }
    public string VariantName { get; set; } = null!;
    public decimal? Price { get; set; }
    public bool IsActive { get; set; }
    public JsonDocument? Attributes { get; set; }
    public DateTime CreatedAt { get; set; }
}
