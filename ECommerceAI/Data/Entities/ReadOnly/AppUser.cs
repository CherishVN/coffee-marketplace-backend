namespace ECommerceAI.Data.Entities.ReadOnly;

// Đặt tên AppUser để tránh conflict với system User
public class AppUser
{
    public Guid Id { get; set; }
    public string? UserCode { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public short? RoleId { get; set; }
    public short Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
