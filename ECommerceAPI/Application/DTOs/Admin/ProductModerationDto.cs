namespace ECommerceAPI.Application.DTOs.Admin;

public class ProductModerationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public short Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> ImageUrls { get; set; } = new();
}

public class HideProductDto
{
    public string Reason { get; set; } = string.Empty;
}

public class RemoveProductDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ProductModerationListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<ProductModerationDto> Products { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ProductModerationResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public ProductModerationDto? Product { get; set; }
}
