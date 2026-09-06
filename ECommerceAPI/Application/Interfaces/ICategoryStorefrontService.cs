using ECommerceAPI.Application.DTOs.Storefront;

namespace ECommerceAPI.Application.Interfaces;

public interface ICategoryStorefrontService
{
    Task<CategoryStorefrontListResponseDto> GetCategoriesAsync(
        int page,
        int pageSize,
        short? level = null);

    Task<CategoryStorefrontTreeResponseDto> GetCategoryTreeAsync();

    Task<CategoryStorefrontDetailResponseDto> GetCategoryByIdAsync(long categoryId);
}
