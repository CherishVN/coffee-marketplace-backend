namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Trạng thái đơn hàng
/// </summary>
public enum OrderStatus : short
{
    /// <summary>
    /// Chờ thanh toán
    /// </summary>
    PendingPayment = 0,
    
    /// <summary>
    /// Chờ xác nhận
    /// </summary>
    PendingConfirmation = 1,
    
    /// <summary>
    /// Đã xác nhận
    /// </summary>
    Confirmed = 2,
    
    /// <summary>
    /// Đang chuẩn bị hàng
    /// </summary>
    Processing = 3,
    
    /// <summary>
    /// Đang giao hàng
    /// </summary>
    Shipping = 4,
    
    /// <summary>
    /// Đã giao hàng
    /// </summary>
    Delivered = 5,
    
    /// <summary>
    /// Hoàn thành
    /// </summary>
    Completed = 6,
    
    /// <summary>
    /// Đã hủy
    /// </summary>
    Cancelled = 7,
    
    /// <summary>
    /// Đã hoàn tiền
    /// </summary>
    Refunded = 8,
    /// <summary>
    /// Đang trả hàng (về cho seller)
    /// </summary>
    Returning = 9,

    /// <summary>
    /// Đã nhận hàng trả (seller đã nhận)
    /// </summary>
    Returned = 10
}
