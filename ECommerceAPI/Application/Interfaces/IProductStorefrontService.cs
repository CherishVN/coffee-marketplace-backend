using ECommerceAPI.Application.DTOs.Storefront;

namespace ECommerceAPI.Application.Interfaces;

public interface IProductStorefrontService
{
    Task<ProductStorefrontListResponseDto> GetProductsAsync(
        int page,
        int pageSize,
        long? categoryId = null,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sortBy = null);

    Task<ProductStorefrontDetailResponseDto> GetProductByIdAsync(Guid productId);
}
