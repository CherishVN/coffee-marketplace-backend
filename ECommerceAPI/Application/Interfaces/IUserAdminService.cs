using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IUserAdminService
{
    Task<UserListResponseDto> GetAllUsersAsync(int page, int pageSize, string? role, short? status);
    Task<UserResponseDto> GetUserByIdAsync(Guid userId);
    Task<UserResponseDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, Guid editorId);
    Task<UserResponseDto> SuspendUserAsync(Guid userId, SuspendUserDto dto, Guid adminId);
    Task<UserResponseDto> UnsuspendUserAsync(Guid userId, Guid adminId);
    Task<UserResponseDto> UpdateUserAccountStatusAsync(Guid userId, UpdateUserAccountStatusDto dto, Guid adminId);
    Task<AuditLogResponseDto> GetUserAuditLogsAsync(Guid userId);
    Task<UserAddressesResponseDto> GetUserAddressesAsync(Guid userId);
    Task<UserWalletDetailResponseDto> GetUserWalletDetailsAsync(Guid userId);
    Task<UserProductReviewsResponseDto> GetUserProductReviewsAsync(Guid userId, int page, int pageSize);
    Task<UserShopReviewsResponseDto> GetUserShopReviewsAsync(Guid userId, int page, int pageSize);
    Task<SimpleMessageResponseDto> SendPasswordResetEmailAsync(Guid userId);
}
