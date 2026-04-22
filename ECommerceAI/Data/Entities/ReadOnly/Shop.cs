namespace ECommerceAI.Data.Entities.ReadOnly;

public class Shop
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = null!;
    public short Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? PrimaryCategoryId { get; set; }
}
