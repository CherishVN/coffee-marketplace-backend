namespace ECommerceAPI.Application.DTOs.Admin;

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public short Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public bool HasOrders { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public Guid? SuspendedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateUserDto
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Role { get; set; }
}

public class SuspendUserDto
{
    public string Reason { get; set; } = string.Empty;
}

public class UserListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<AdminUserDto> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class UserResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public AdminUserDto? User { get; set; }
}

public class UserAuditLogDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid EditorId { get; set; }
    public string? EditorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<UserAuditLogDto> Logs { get; set; } = new();
}
