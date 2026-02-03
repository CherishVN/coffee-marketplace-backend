using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface ISellerApprovalService
{
    Task<ShopListResponseDto> GetPendingShopsAsync(int page, int pageSize, short? verificationStatus);
    Task<ShopResponseDto> GetShopByIdAsync(Guid shopId);
    Task<ShopResponseDto> ApproveShopAsync(Guid shopId, ApproveSellerDto dto, Guid adminId);
    Task<ShopResponseDto> RejectShopAsync(Guid shopId, RejectSellerDto dto, Guid adminId);
    Task<ShopResponseDto> ActivateShopAsync(Guid shopId, Guid adminId);
    Task<ShopResponseDto> SuspendShopAsync(Guid shopId, SuspendShopDto dto, Guid adminId);
    Task<ShopResponseDto> CloseShopAsync(Guid shopId, SuspendShopDto dto, Guid adminId);
}
