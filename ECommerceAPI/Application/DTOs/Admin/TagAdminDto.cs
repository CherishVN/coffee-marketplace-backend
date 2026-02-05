namespace ECommerceAPI.Application.DTOs.Admin;

public class TagDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ProductCount { get; set; }
}

public class CreateTagDto
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateTagDto
{
    public string Name { get; set; } = string.Empty;
}

public class TagListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<TagDto> Tags { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class TagResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public TagDto? Tag { get; set; }
}
