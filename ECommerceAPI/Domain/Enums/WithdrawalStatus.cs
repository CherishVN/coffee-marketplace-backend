namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái yêu cầu rút tiền
/// </summary>
public enum WithdrawalStatus : short
{
    /// <summary>
    /// Chờ duyệt
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Đã duyệt
    /// </summary>
    Approved = 1,
    
    /// <summary>
    /// Bị từ chối
    /// </summary>
    Rejected = 2,
    
    /// <summary>
    /// Đang xử lý
    /// </summary>
    Processing = 3,
    
    /// <summary>
    /// Đã thanh toán
    /// </summary>
    Paid = 4,
    
    /// <summary>
    /// Đã hủy
    /// </summary>
    Cancelled = 5
}
