namespace ECommerceAPI.Application.DTOs.Admin;

public class CategoryDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string? ParentName { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public short Level { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
    public int SubcategoryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CategoryDto>? Subcategories { get; set; }
}

public class CreateCategoryDto
{
    public long? ParentId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class UpdateCategoryDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public long? ParentId { get; set; }
    public bool? MigrateProducts { get; set; }
}

public class ToggleCategoryStatusDto
{
    public string? Reason { get; set; }
}

public class MigrateProductsDto
{
    public long TargetCategoryId { get; set; }
}

public class CategoryListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CategoryDto> Categories { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CategoryResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public CategoryDto? Category { get; set; }
}

public class CategoryTreeResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CategoryDto> Tree { get; set; } = new();
}

public class MigrateProductsResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int MigratedCount { get; set; }
}
