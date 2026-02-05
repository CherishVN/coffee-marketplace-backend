using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IProductModerationService
{
    Task<ProductModerationListResponseDto> GetAllProductsAsync(
        int page, 
        int pageSize, 
        short? status = null,
        Guid? shopId = null,
        string? search = null);
        
    Task<ProductModerationResponseDto> GetProductByIdAsync(Guid productId);
    Task<ProductModerationResponseDto> HideProductAsync(Guid productId, HideProductDto dto, Guid adminId);
    Task<ProductModerationResponseDto> UnhideProductAsync(Guid productId, Guid adminId);
    Task<ProductModerationResponseDto> RemoveProductAsync(Guid productId, RemoveProductDto dto, Guid adminId);
}
