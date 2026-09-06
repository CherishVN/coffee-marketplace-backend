namespace ECommerceAPI.Application.DTOs.Seller;

/// <summary>
/// Kết quả quyết toán ví sau thanh toán đơn (để thông báo / hiển thị).
/// </summary>
public sealed class SellerSettlementCreditResult
{
    public Guid SellerId { get; init; }
    /// <summary>Tiền thực cộng vào available (sau phí sàn).</summary>
    public decimal NetAmount { get; init; }
    public decimal GrossSubtotal { get; init; }
    public decimal PlatformFeeAmount { get; init; }
    public decimal CommissionPercent { get; init; }
}
