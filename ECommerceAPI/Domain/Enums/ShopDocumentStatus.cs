namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái tài liệu cửa hàng
/// </summary>
public enum ShopDocumentStatus : short
{
    /// <summary>
    /// Chờ xác minh
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Đã xác minh
    /// </summary>
    Approved = 1,
    
    /// <summary>
    /// Bị từ chối
    /// </summary>
    Rejected = 2
}
