namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái xác minh cửa hàng
/// </summary>
public enum ShopVerificationStatus : short
{
    /// <summary>
    /// Chờ xác minh
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Đã xác minh
    /// </summary>
    Verified = 1,
    
    /// <summary>
    /// Bị từ chối
    /// </summary>
    Rejected = 2
}
