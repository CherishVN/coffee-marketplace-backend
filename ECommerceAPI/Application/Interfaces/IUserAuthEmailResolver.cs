using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IUserAuthEmailResolver
{
    Task<string?> GetEmailByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<string?> GetAvatarUrlByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<SupabaseAuthEnrichmentDto?> GetSupabaseAuthEnrichmentAsync(Guid userId, CancellationToken cancellationToken = default);
}
