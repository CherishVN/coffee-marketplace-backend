namespace ECommerceAPI.Application.Interfaces;

public interface ISellerWalletReleaseService
{
    /// <summary>Chuyển tiền quyết toán từ held sang available khi đơn hoàn thành. Idempotent theo ledger order_release.</summary>
    Task<bool> TryReleaseSettlementForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
