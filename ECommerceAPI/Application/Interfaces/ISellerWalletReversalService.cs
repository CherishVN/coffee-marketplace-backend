namespace ECommerceAPI.Application.Interfaces;

/// <summary>
/// Hoàn tác quyết toán ví seller (và phí sàn) khi đơn hủy / hoàn tiền sau khi đã ghi có.
/// </summary>
public interface ISellerWalletReversalService
{
    /// <summary>
    /// Nếu đơn đã có ghi có order_settlement và chưa hoàn tác: trừ lại ví, ledger âm, đánh dấu platform_fee reversed.
    /// Idempotent. Không gọi SaveChanges.
    /// </summary>
    Task<bool> TryReverseSettlementForOrderAsync(Guid orderId, string reason, CancellationToken cancellationToken = default);
}
