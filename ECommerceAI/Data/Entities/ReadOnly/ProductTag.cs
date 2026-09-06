namespace ECommerceAI.Data.Entities.ReadOnly;

public class ProductTag
{
    public Guid ProductId { get; set; }
    public long TagId { get; set; }

    public virtual Product Product { get; set; } = null!;
    public virtual Tag Tag { get; set; } = null!;
}
