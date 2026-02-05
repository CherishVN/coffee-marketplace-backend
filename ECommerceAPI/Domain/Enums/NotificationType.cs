namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Loại thông báo
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Thông báo hệ thống
    /// </summary>
    System,
    
    /// <summary>
    /// Thông báo đơn hàng
    /// </summary>
    Order,
    
    /// <summary>
    /// Thông báo thanh toán
    /// </summary>
    Payment,
    
    /// <summary>
    /// Thông báo khiếu nại
    /// </summary>
    Dispute,
    
    /// <summary>
    /// Thông báo khuyến mãi
    /// </summary>
    Promotion,
    
    /// <summary>
    /// Thông báo đánh giá
    /// </summary>
    Review,
    
    /// <summary>
    /// Thông báo chat
    /// </summary>
    Chat,
    
    /// <summary>
    /// Thông báo cửa hàng
    /// </summary>
    Shop
}
