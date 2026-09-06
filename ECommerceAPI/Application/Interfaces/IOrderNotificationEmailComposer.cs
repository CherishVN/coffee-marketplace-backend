using ECommerceAPI.Domain.Enums;

namespace ECommerceAPI.Application.Interfaces;

/// <summary>
/// Tạo HTML + tiêu đề email thông báo trạng thái đơn hàng (giao diện dạng thương mại điện tử).
/// </summary>
public interface IOrderNotificationEmailComposer
{
    /// <returns>null nếu không tìm thấy đơn.</returns>
    Task<(string Subject, string Html)?> TryComposeAsync(
        Guid orderId,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        CancellationToken cancellationToken = default);
}
