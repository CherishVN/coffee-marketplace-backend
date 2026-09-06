namespace ECommerceAI.Data.Entities.ReadOnly;

public class Category
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public short Level { get; set; }
    public bool IsActive { get; set; }
}
