namespace ECommerceAPI.Application.DTOs.Storefront;

/// <summary>
/// Dùng cho danh sách sản phẩm. Rating/review tùy endpoint (có thể 0 nếu không tính).
/// </summary>
public class ProductStorefrontDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string ShopSlug { get; set; } = string.Empty;
    public string? ShopLogoUrl { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "VND";
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategorySlug { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public int SoldCount { get; set; }
    /// <summary>Điểm trung bình (0 nếu chưa có đánh giá) — dùng hiển thị thẻ sản phẩm & sắp xếp relevance.</summary>
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

/// <summary>
/// Thông tin local specialty gắn với sản phẩm (hiển thị ở trang chi tiết).
/// </summary>
public class ProductLocalMetaDto
{
    public int ProfileId { get; set; }
    public string ProvinceName { get; set; } = string.Empty;
    public string ArchetypeName { get; set; } = string.Empty;
    public string? DisplayNote { get; set; }
    public List<string> SelectedTraits { get; set; } = new();
    public List<string> ExpectedTraits { get; set; } = new();
}

/// <summary>
/// Dùng cho trang chi tiết sản phẩm (có đầy đủ thông tin)
/// </summary>
public class ProductStorefrontDetailDto : ProductStorefrontDto
{
    public string? Description { get; set; }
    public List<ProductVariantStorefrontDto> Variants { get; set; } = new();
    public int TotalStock { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Materials { get; set; } = new();

    /// <summary>Null nếu sản phẩm không gắn với hồ sơ đặc sản nào.</summary>
    public ProductLocalMetaDto? LocalMeta { get; set; }
}

public class ProductVariantStorefrontDto
{
    public Guid Id { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string? Attributes { get; set; }
    public decimal? Price { get; set; }
    public bool IsActive { get; set; }
    public int StockQuantity { get; set; }
}

public class ProductStorefrontListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<ProductStorefrontDto> Products { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ProductStorefrontDetailResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public ProductStorefrontDetailDto? Product { get; set; }
}

public class ProductStockBatchRequestDto
{
    /// <summary>List of product IDs to fetch stock for (max 50)</summary>
    public List<Guid> ProductIds { get; set; } = new();
}

public class VariantStockDto
{
    public Guid VariantId { get; set; }
    public int Stock { get; set; }
}

public class ProductStockDto
{
    public Guid ProductId { get; set; }
    public int TotalStock { get; set; }
    public List<VariantStockDto> Variants { get; set; } = new();
}

public class ProductStockBatchResponseDto
{
    public bool Success { get; set; }
    public List<ProductStockDto> Items { get; set; } = new();
}
