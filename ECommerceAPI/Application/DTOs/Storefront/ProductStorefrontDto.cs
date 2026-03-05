namespace ECommerceAPI.Application.DTOs.Storefront;

/// <summary>
/// Dùng cho danh sách sản phẩm (không cần rating/review)
/// </summary>
public class ProductStorefrontDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "VND";
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategorySlug { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Dùng cho trang chi tiết sản phẩm (có đầy đủ thông tin)
/// </summary>
public class ProductStorefrontDetailDto : ProductStorefrontDto
{
    public string? Description { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public List<ProductVariantStorefrontDto> Variants { get; set; } = new();
}

public class ProductVariantStorefrontDto
{
    public Guid Id { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public bool IsActive { get; set; }
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
