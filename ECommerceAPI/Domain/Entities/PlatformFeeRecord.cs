namespace ECommerceAPI.Domain.Entities;

/// <summary>
/// Ghi nhận phí sàn theo từng đơn đã quyết toán ví (một dòng / order).
/// </summary>
public partial class PlatformFeeRecord
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid? PaymentId { get; set; }

    public Guid ShopId { get; set; }

    public Guid SellerId { get; set; }

    public decimal GrossSubtotal { get; set; }

    public decimal CommissionPercent { get; set; }

    public decimal FeeAmount { get; set; }

    public decimal NetToSeller { get; set; }

    public string Currency { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    /// <summary>Khi hoàn tác quyết toán (hủy/hoàn đơn), báo cáo phí sàn bỏ qua bản ghi đã reversed.</summary>
    public DateTime? ReversedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Shop Shop { get; set; } = null!;

    public virtual Payment? Payment { get; set; }

    public virtual User Seller { get; set; } = null!;
}
