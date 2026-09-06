using System;

namespace ECommerceAPI.Domain.Entities;

/// <summary>
/// Metadata "local specialty" gắn với sản phẩm.
/// Một sản phẩm có tối đa 1 bản ghi ở đây (1-1 với Product).
/// </summary>
public class ProductLocalMeta
{
    public Guid ProductId { get; set; }

    public int LocalSpecialtyProfileId { get; set; }

    /// <summary>
    /// Các đặc điểm seller đã chọn / tick (pipe-separated),
    /// vd: "Đắng đậm|Ít chua|Caffeine cao".
    /// </summary>
    public string SelectedTraitsPipe { get; set; } = string.Empty;

    /// <summary>
    /// Cảnh báo mâu thuẫn (nếu có) do rule/AI phát hiện.
    /// Null = không có vấn đề.
    /// </summary>
    public string? MismatchWarning { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Product Product { get; set; } = null!;
    public virtual LocalSpecialtyProfile LocalSpecialtyProfile { get; set; } = null!;
}
