namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái sản phẩm
/// </summary>
public enum ProductStatus : short
{
    /// <summary>
    /// Bản nháp
    /// </summary>
    Draft = 0,
    
    /// <summary>
    /// Đang hoạt động/Đang bán
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// Tạm ẩn
    /// </summary>
    Hidden = 2,
    
    /// <summary>
    /// Hết hàng
    /// </summary>
    OutOfStock = 3,
    
    /// <summary>
    /// Bị gỡ bởi admin
    /// </summary>
    Removed = 4,

    /// <summary>
    /// Chờ admin duyệt (tạo mới hoặc cập nhật từ seller)
    /// </summary>
    PendingApproval = 5
}
