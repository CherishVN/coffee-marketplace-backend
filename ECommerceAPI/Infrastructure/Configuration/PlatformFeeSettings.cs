namespace ECommerceAPI.Infrastructure.Configuration;

/// <summary>
/// Phí sàn / hoa hồng trên tiền hàng (subtotal) khi ghi có ví seller.
/// </summary>
public class PlatformFeeSettings
{
    public const string SectionName = "PlatformFee";

    /// <summary>
    /// Phần trăm giữ lại cho sàn (0–100). Ví dụ 10 = seller nhận 90% subtotal.
    /// </summary>
    public decimal CommissionPercent { get; set; } = 10;
}
