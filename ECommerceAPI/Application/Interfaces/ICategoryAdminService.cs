using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface ICategoryAdminService
{
    Task<CategoryListResponseDto> GetAllCategoriesAsync(int page, int pageSize, short? level, bool? isActive);
    Task<CategoryTreeResponseDto> GetCategoryTreeAsync();
    Task<CategoryResponseDto> GetCategoryByIdAsync(long categoryId);
    Task<CategoryResponseDto> CreateCategoryAsync(CreateCategoryDto dto, Guid adminId);
    Task<CategoryResponseDto> UpdateCategoryAsync(long categoryId, UpdateCategoryDto dto, Guid adminId);
    Task<CategoryResponseDto> ActivateCategoryAsync(long categoryId, Guid adminId);
    Task<CategoryResponseDto> DeactivateCategoryAsync(long categoryId, ToggleCategoryStatusDto dto, Guid adminId);
    Task<CategoryResponseDto> DeleteCategoryAsync(long categoryId, Guid adminId);
    Task<MigrateProductsResponseDto> MigrateProductsAsync(long sourceCategoryId, MigrateProductsDto dto, Guid adminId);
}
