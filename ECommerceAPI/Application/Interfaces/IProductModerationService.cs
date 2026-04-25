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

    /// <summary>Phê duyệt sản phẩm (chờ duyệt → đang bán).</summary>
    Task<ProductModerationResponseDto> ApproveProductAsync(Guid productId, Guid adminId);

    /// <summary>Từ chối duyệt (chờ duyệt → nháp để seller sửa).</summary>
    Task<ProductModerationResponseDto> RejectProductAsync(
        Guid productId,
        RejectProductDto dto,
        Guid adminId);
}
