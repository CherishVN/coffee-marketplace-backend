namespace ECommerceAI.Data.Entities.ReadOnly;

// Read-only entity - map tới bảng products của Main API
public class Product
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public long? CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "VND";
    public short Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public virtual ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
    public virtual Category? Category { get; set; }
}
