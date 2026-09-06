using ECommerceAPI.Application.DTOs.Storefront;

namespace ECommerceAPI.Application.Interfaces;

public interface IShopStorefrontService
{
    Task<ShopPublicDetailResponseDto> GetShopBySlugAsync(string slug, Guid? currentUserId = null);
    Task<ProductStorefrontListResponseDto> GetShopProductsAsync(Guid shopId, int page, int pageSize, string? sortBy = null, long? categoryId = null);
    Task<List<ShopCategoryDto>> GetShopCategoriesAsync(Guid shopId);
    Task<ServiceResponse> FollowShopAsync(Guid userId, Guid shopId);
    Task<ServiceResponse> UnfollowShopAsync(Guid userId, Guid shopId);
    Task<List<ShopFollowedDto>> GetFollowedShopsAsync(Guid userId);
}
