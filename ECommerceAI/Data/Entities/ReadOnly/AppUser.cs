namespace ECommerceAI.Data.Entities.ReadOnly;

// Đặt tên AppUser để tránh conflict với system User
public class AppUser
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public short Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
