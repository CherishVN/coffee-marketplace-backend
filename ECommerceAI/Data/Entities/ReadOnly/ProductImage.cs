namespace ECommerceAI.Data.Entities.ReadOnly;

public class ProductImage
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public int SortOrder { get; set; }
}
