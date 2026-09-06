using ECommerceAPI.Application.DTOs.Seller;
using ECommerceAPI.Domain.Entities;

namespace ECommerceAPI.Application.Interfaces;

/// <summary>
/// Ghi có ví seller khi đơn thanh toán thành công (idempotent theo order).
/// </summary>
public interface ISellerWalletSettlementService
{
    /// <summary>
    /// Cộng tiền hàng (subtotal trừ phí sàn theo cấu hình) vào AvailableBalance. Không gọi SaveChanges.
    /// Luôn ghi ledger theo đơn (kể cả net = 0) để idempotent. Null nếu bỏ qua (subtotal ≤ 0, đã quyết toán, không có shop).
    /// </summary>
    Task<SellerSettlementCreditResult?> CreditSellerForPaidOrderAsync(Order order, Payment payment, CancellationToken cancellationToken = default);
}
