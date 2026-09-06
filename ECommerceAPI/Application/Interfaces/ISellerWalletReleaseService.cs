namespace ECommerceAPI.Application.Interfaces;

public interface ISellerWalletReleaseService
{
    /// <summary>
    /// Giải ngân Held → Available khi đơn đã Hoàn thành, đủ 7 ngày từ lần đầu Đã giao, và không còn khiếu nại mở.
    /// Idempotent theo ledger order_release.
    /// </summary>
    Task<bool> TryReleaseSettlementForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Quét các đơn đủ điều kiện và giải ngân (dùng cho background job).</summary>
    Task<int> ReleaseDueSettlementsAsync(CancellationToken cancellationToken = default);
}
