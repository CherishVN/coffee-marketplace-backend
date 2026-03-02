using ECommerceAPI.Application.DTOs.Reviews;

namespace ECommerceAPI.Application.Interfaces;

public interface IReviewService
{
    Task<ServiceResponse<ProductReviewDto>> CreateProductReviewAsync(Guid userId, CreateProductReviewDto dto);

    Task<ProductReviewListResponseDto> GetProductReviewsAsync(
        Guid productId,
        int page,
        int pageSize,
        string? sortBy = null);
}

