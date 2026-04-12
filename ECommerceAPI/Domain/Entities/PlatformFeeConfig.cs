namespace ECommerceAPI.Domain.Entities;

/// <summary>
/// Lưu lịch sử thay đổi tỷ lệ phí sàn. Bản ghi mới nhất là tỷ lệ đang áp dụng.
/// </summary>
public partial class PlatformFeeConfig
{
    public Guid Id { get; set; }

    /// <summary>Tỷ lệ phí sàn (0–100). Ví dụ: 10 = seller nhận 90%.</summary>
    public decimal CommissionPercent { get; set; }

    /// <summary>Admin đã thay đổi tỷ lệ.</summary>
    public Guid ChangedBy { get; set; }

    /// <summary>Ghi chú lý do thay đổi.</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Admin { get; set; } = null!;
}
