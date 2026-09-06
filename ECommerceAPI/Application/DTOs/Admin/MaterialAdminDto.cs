namespace ECommerceAPI.Application.DTOs.Admin;

public class MaterialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ProductCount { get; set; }
}

public class CreateMaterialDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateMaterialDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class MaterialListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<MaterialDto> Materials { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class MaterialResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public MaterialDto? Material { get; set; }
}
