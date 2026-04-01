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
        double? minRating = null,
        string? sortBy = null,
        List<long>? tagIds = null,
        List<Guid>? materialIds = null);

    Task<ProductStorefrontDetailResponseDto> GetProductByIdAsync(Guid productId);
    Task<ProductStorefrontDetailResponseDto> GetProductBySlugAsync(string slug);

    /// <summary>Lấy sản phẩm gợi ý: trending (nhiều lượt bán) + mới nhất</summary>
    Task<ProductStorefrontListResponseDto> GetSuggestionsAsync(int limit = 10);
}
