namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Vai trò người gửi tin nhắn trong khiếu nại
/// </summary>
public enum DisputeSenderRole : short
{
    /// <summary>
    /// Khách hàng
    /// </summary>
    Customer = 0,
    
    /// <summary>
    /// Người bán
    /// </summary>
    Seller = 1,
    
    /// <summary>
    /// Quản trị viên
    /// </summary>
    Admin = 2
}
