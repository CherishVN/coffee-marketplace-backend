using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class FavoriteService : IFavoriteService
{
    private readonly ApplicationDbContext _context;

    public FavoriteService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Guid>> GetFavoriteIdsAsync(Guid userId)
    {
        return await _context.FavoriteProducts
            .Where(f => f.UserId == userId)
            .Select(f => f.ProductId)
            .ToListAsync();
    }

    public async Task<bool> IsFavoritedAsync(Guid userId, Guid productId)
    {
        return await _context.FavoriteProducts
            .AnyAsync(f => f.UserId == userId && f.ProductId == productId);
    }

    public async Task<bool> AddFavoriteAsync(Guid userId, Guid productId)
    {
        var exists = await _context.FavoriteProducts
            .AnyAsync(f => f.UserId == userId && f.ProductId == productId);

        if (exists) return true;

        var productExists = await _context.Products.AnyAsync(p => p.Id == productId);
        if (!productExists) return false;

        _context.FavoriteProducts.Add(new FavoriteProduct
        {
            UserId = userId,
            ProductId = productId,
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFavoriteAsync(Guid userId, Guid productId)
    {
        var favorite = await _context.FavoriteProducts
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId);

        if (favorite is null) return false;

        _context.FavoriteProducts.Remove(favorite);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleFavoriteAsync(Guid userId, Guid productId)
    {
        var existing = await _context.FavoriteProducts
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId);

        if (existing is not null)
        {
            _context.FavoriteProducts.Remove(existing);
            await _context.SaveChangesAsync();
            return false;
        }

        var productExists = await _context.Products.AnyAsync(p => p.Id == productId);
        if (!productExists) return false;

        _context.FavoriteProducts.Add(new FavoriteProduct
        {
            UserId = userId,
            ProductId = productId,
        });

        await _context.SaveChangesAsync();
        return true;
    }
}
