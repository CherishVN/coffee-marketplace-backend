using System;
using System.Collections.Generic;

namespace ECommerceAPI.Application.DTOs.Storefront;

public class CollectionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }
    public string? ShortDesc { get; set; }
    public string? Image { get; set; }
    public string? ImageAlt { get; set; }
    public string? Type { get; set; }

    /// <summary>Up to 8 preview products for the home page row.</summary>
    public List<ProductStorefrontDto> Products { get; set; } = new();
}
