namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái cửa hàng
/// </summary>
public enum ShopStatus : short
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
    /// Bị đình chỉ
    /// </summary>
    Suspended = 2,
    
    /// <summary>
    /// Đã đóng cửa
    /// </summary>
    Closed = 3
}
