namespace ECommerceAPI.Domain.Entities;

public class UserAuditLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid EditorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual User Editor { get; set; } = null!;
}
