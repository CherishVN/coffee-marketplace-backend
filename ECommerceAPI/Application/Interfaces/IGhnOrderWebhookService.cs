using ECommerceAPI.Application.DTOs.Webhooks;

namespace ECommerceAPI.Application.Interfaces;

public interface IGhnOrderWebhookService
{
    /// <summary>
    /// Xử lý callback trạng thái vận đơn từ GHN. Trả về mô tả nội bộ (log); HTTP luôn 200 nếu gọi thành công.
    /// </summary>
    Task<GhnOrderWebhookResult> ProcessOrderStatusAsync(
        GhnOrderStatusPayload payload,
        bool validateShopId,
        CancellationToken cancellationToken = default);
}

public sealed class GhnOrderWebhookResult
{
    public bool Handled { get; init; }
    public string? Message { get; init; }
    public Guid? OrderId { get; init; }
}
