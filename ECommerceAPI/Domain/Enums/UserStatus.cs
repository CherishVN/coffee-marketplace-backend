namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái tài khoản người dùng
/// </summary>
public enum UserStatus : short
{
    /// <summary>
    /// Chưa kích hoạt
    /// </summary>
    Inactive = 0,
    
    /// <summary>
    /// Đang hoạt động
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// Bị khóa/đình chỉ
    /// </summary>
    Suspended = 2,
    
    /// <summary>
    /// Đã xóa
    /// </summary>
    Deleted = 3
}
