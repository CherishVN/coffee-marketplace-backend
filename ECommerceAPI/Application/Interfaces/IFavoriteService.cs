namespace ECommerceAPI.Application.Interfaces;

public interface IFavoriteService
{
    Task<List<Guid>> GetFavoriteIdsAsync(Guid userId);
    Task<bool> AddFavoriteAsync(Guid userId, Guid productId);
    Task<bool> RemoveFavoriteAsync(Guid userId, Guid productId);
    Task<bool> ToggleFavoriteAsync(Guid userId, Guid productId);
    Task<bool> IsFavoritedAsync(Guid userId, Guid productId);
}
