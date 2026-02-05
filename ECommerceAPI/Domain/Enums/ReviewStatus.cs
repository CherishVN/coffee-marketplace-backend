namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái đánh giá
/// </summary>
public enum ReviewStatus : short
{
    /// <summary>
    /// Chờ duyệt
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Đã duyệt/Hiển thị
    /// </summary>
    Approved = 1,
    
    /// <summary>
    /// Bị ẩn
    /// </summary>
    Hidden = 2,
    
    /// <summary>
    /// Bị xóa
    /// </summary>
    Removed = 3
}
