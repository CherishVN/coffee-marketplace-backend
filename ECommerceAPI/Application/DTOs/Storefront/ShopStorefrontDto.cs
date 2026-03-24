namespace ECommerceAPI.Application.DTOs.Storefront;

public class ShopPublicDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? CoverUrl { get; set; }
    public int ProductCount { get; set; }
    public int FollowerCount { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsFollowing { get; set; }
}

public class ShopPublicDetailResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public ShopPublicDto? Shop { get; set; }
}

public class ShopCategoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public int ProductCount { get; set; }
}
