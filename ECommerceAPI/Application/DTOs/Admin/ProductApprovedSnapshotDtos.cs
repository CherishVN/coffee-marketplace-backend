namespace ECommerceAPI.Application.DTOs.Admin;

/// <summary>Ảnh chụp sản phẩm tại thời điểm được duyệt (dùng so sánh với bản đang xin duyệt). JSON lưu trong products.last_approved_snapshot_json</summary>
public class ProductApprovedSnapshotDto
{
    public string? CapturedAtUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public List<long> TagIds { get; set; } = new();
    public List<string> TagNames { get; set; } = new();
    public List<Guid> MaterialIds { get; set; } = new();
    public List<string> MaterialNames { get; set; } = new();
    public int? BaseInventoryQuantity { get; set; }
    public List<ProductApprovedVariantSnapshotDto> Variants { get; set; } = new();
}

public class ProductApprovedVariantSnapshotDto
{
    public string VariantName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public int Stock { get; set; }
    public string? Attributes { get; set; }
}
