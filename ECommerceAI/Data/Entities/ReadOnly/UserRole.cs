namespace ECommerceAI.Data.Entities.ReadOnly;

public class UserReadOnly
{
    public Guid Id { get; set; }
    public short? RoleId { get; set; }
    public RoleReadOnly? Role { get; set; }
}

public class RoleReadOnly
{
    public short Id { get; set; }
    public string Code { get; set; } = null!;
}
