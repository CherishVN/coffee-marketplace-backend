namespace ECommerceAI.Data.Entities.ReadOnly;

public class Tag
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
