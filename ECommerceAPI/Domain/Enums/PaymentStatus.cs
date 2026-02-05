namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái thanh toán
/// </summary>
public enum PaymentStatus : short
{
    /// <summary>
    /// Chờ thanh toán
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Đã thanh toán
    /// </summary>
    Paid = 1,
    
    /// <summary>
    /// Thanh toán thất bại
    /// </summary>
    Failed = 2,
    
    /// <summary>
    /// Đã hoàn tiền
    /// </summary>
    Refunded = 3,
    
    /// <summary>
    /// Đã hủy
    /// </summary>
    Cancelled = 4
}
