namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Loại khiếu nại
/// </summary>
public enum DisputeType : short
{
    /// <summary>
    /// Yêu cầu hoàn tiền
    /// </summary>
    Refund = 0,
    
    /// <summary>
    /// Yêu cầu trả hàng
    /// </summary>
    Return = 1,
    
    /// <summary>
    /// Hàng bị hư hỏng
    /// </summary>
    Damaged = 2,
    
    /// <summary>
    /// Không nhận được hàng
    /// </summary>
    NotReceived = 3,
    
    /// <summary>
    /// Giao sai hàng
    /// </summary>
    WrongItem = 4,
    
    /// <summary>
    /// Vấn đề chất lượng
    /// </summary>
    QualityIssue = 5,
    
    /// <summary>
    /// Khác
    /// </summary>
    Other = 6
}
