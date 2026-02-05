namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái giỏ hàng
/// </summary>
public enum CartStatus : short
{
    /// <summary>
    /// Đang hoạt động
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// Đã chuyển thành đơn hàng
    /// </summary>
    CheckedOut = 1,
    
    /// <summary>
    /// Bị bỏ quên
    /// </summary>
    Abandoned = 2
}
