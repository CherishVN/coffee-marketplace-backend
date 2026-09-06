using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IUserAuthEmailResolver
{
    Task<string?> GetEmailByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<string?> GetAvatarUrlByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<SupabaseAuthEnrichmentDto?> GetSupabaseAuthEnrichmentAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa cache snapshot Supabase của 1 user. Gọi khi user vừa cập nhật
    /// avatar/email/metadata để lần đọc tiếp theo lấy dữ liệu mới ngay.
    /// </summary>
    void InvalidateUser(Guid userId);
}
