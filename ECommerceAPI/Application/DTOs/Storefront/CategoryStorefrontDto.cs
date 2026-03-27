namespace ECommerceAPI.Application.DTOs.Storefront;

public class CategoryStorefrontDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public short Level { get; set; }
    public int ProductCount { get; set; }
    public string? Image { get; set; }
    public List<CategoryStorefrontDto> Subcategories { get; set; } = new();
}

public class CategoryStorefrontListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CategoryStorefrontDto> Categories { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CategoryStorefrontTreeResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CategoryStorefrontDto> Tree { get; set; } = new();
}

public class CategoryStorefrontDetailResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public CategoryStorefrontDto? Category { get; set; }
}
