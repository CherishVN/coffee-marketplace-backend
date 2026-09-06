using ECommerceAPI.Application.DTOs.Reviews;

namespace ECommerceAPI.Application.Interfaces;

public interface IReviewService
{
    Task<ServiceResponse<ProductReviewDto>> CreateProductReviewAsync(Guid userId, CreateProductReviewDto dto);

    Task<ProductReviewListResponseDto> GetProductReviewsAsync(
        Guid productId,
        int page,
        int pageSize,
        string? sortBy = null,
        short? rating = null,
        bool? hasComment = null,
        bool? hasImage = null);

    Task<ProductReviewStatsResponseDto> GetProductReviewStatsAsync(Guid productId);

    Task<ServiceResponse<ShopReviewDto>> CreateShopReviewAsync(Guid userId, CreateShopReviewDto dto);

    Task<ShopReviewListResponseDto> GetShopReviewsAsync(
        Guid shopId,
        int page,
        int pageSize,
        string? sortBy = null);
}

