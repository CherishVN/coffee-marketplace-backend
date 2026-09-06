using System.Collections.Generic;

namespace ECommerceAPI.Application.DTOs.Storefront;

public class HomeResponseDto
{
    public List<BannerDto> Banners { get; set; } = new();
    public List<CollectionDto> Collections { get; set; } = new();
}
