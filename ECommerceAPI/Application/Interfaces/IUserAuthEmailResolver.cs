namespace ECommerceAPI.Application.Interfaces;

/// <summary>
/// Lấy email đăng nhập từ Supabase Auth (admin API) theo user id.
/// </summary>
public interface IUserAuthEmailResolver
{
    Task<string?> GetEmailByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<string?> GetAvatarUrlByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
