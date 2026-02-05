namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái khiếu nại
/// </summary>
public enum DisputeStatus : short
{
    /// <summary>
    /// Chờ xử lý
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Đang xem xét
    /// </summary>
    UnderReview = 1,
    
    /// <summary>
    /// Chờ seller phản hồi
    /// </summary>
    WaitingSeller = 2,
    
    /// <summary>
    /// Chờ customer phản hồi
    /// </summary>
    WaitingCustomer = 3,
    
    /// <summary>
    /// Đã giải quyết
    /// </summary>
    Resolved = 4,
    
    /// <summary>
    /// Bị từ chối
    /// </summary>
    Rejected = 5,
    
    /// <summary>
    /// Đã hoàn tiền
    /// </summary>
    Refunded = 6,
    
    /// <summary>
    /// Đã hủy
    /// </summary>
    Cancelled = 7
}
