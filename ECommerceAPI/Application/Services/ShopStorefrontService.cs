using ECommerceAPI.Application.DTOs.Storefront;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class ShopStorefrontService : IShopStorefrontService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ShopStorefrontService> _logger;

    public ShopStorefrontService(ApplicationDbContext context, ILogger<ShopStorefrontService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ShopPublicDetailResponseDto> GetShopBySlugAsync(string slug, Guid? currentUserId = null)
    {
        try
        {
            var shop = await _context.Shops
                .Where(s => s.Slug == slug && s.Status == 1 && s.VerificationStatus == 1)
                .Select(s => new ShopPublicDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Slug = s.Slug,
                    Description = s.Description,
                    LogoUrl = s.LogoUrl,
                    CoverUrl = s.CoverUrl,
                    ProductCount = s.Products.Count(p => p.Status == (short)ProductStatus.Active),
                    FollowerCount = s.ShopFollows.Count,
                    AverageRating = s.ShopReviews.Any()
                        ? Math.Round(s.ShopReviews.Average(r => (double)r.Rating), 1)
                        : 0,
                    ReviewCount = s.ShopReviews.Count,
                    CreatedAt = s.CreatedAt,
                    IsFollowing = currentUserId.HasValue
                        && s.ShopFollows.Any(f => f.UserId == currentUserId.Value),
                })
                .FirstOrDefaultAsync();

            if (shop is null)
                return new ShopPublicDetailResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy shop"
                };

            return new ShopPublicDetailResponseDto { Success = true, Shop = shop };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching shop by slug {Slug}", slug);
            return new ShopPublicDetailResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy thông tin shop"
            };
        }
    }

    public async Task<ProductStorefrontListResponseDto> GetShopProductsAsync(
        Guid shopId, int page, int pageSize, string? sortBy = null, long? categoryId = null)
    {
        try
        {
            var query = _context.Products
                .Include(p => p.Shop)
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Where(p => p.ShopId == shopId && p.Status == (short)ProductStatus.Active)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            query = sortBy switch
            {
                "price_asc" => query.OrderBy(p => p.BasePrice).ThenBy(p => p.Id),
                "price_desc" => query.OrderByDescending(p => p.BasePrice).ThenBy(p => p.Id),
                "newest" => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
                "best_selling" => query.OrderByDescending(p => p.SoldCount).ThenBy(p => p.Id),
                _ => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            };

            var totalCount = await query.CountAsync();

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductStorefrontDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    ShopId = p.ShopId,
                    ShopName = p.Shop.Name,
                    ShopSlug = p.Shop.Slug,
                    BasePrice = p.BasePrice,
                    Currency = p.Currency,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategorySlug = p.Category != null ? p.Category.Slug : null,
                    ImageUrls = p.ProductImages
                        .OrderBy(img => img.SortOrder)
                        .Select(img => img.ImageUrl)
                        .ToList(),
                    CreatedAt = p.CreatedAt,
                    SoldCount = p.SoldCount,
                })
                .ToListAsync();

            return new ProductStorefrontListResponseDto
            {
                Success = true,
                Products = products,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching shop products for {ShopId}", shopId);
            return new ProductStorefrontListResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy sản phẩm của shop"
            };
        }
    }

    public async Task<List<ShopCategoryDto>> GetShopCategoriesAsync(Guid shopId)
    {
        try
        {
            return await _context.Products
                .Where(p => p.ShopId == shopId && p.Status == (short)ProductStatus.Active && p.CategoryId != null)
                .GroupBy(p => new { p.CategoryId, p.Category!.Name, p.Category.Slug })
                .Select(g => new ShopCategoryDto
                {
                    Id = g.Key.CategoryId!.Value,
                    Name = g.Key.Name,
                    Slug = g.Key.Slug,
                    ProductCount = g.Count()
                })
                .OrderByDescending(c => c.ProductCount)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching shop categories for {ShopId}", shopId);
            return new List<ShopCategoryDto>();
        }
    }

    public async Task<ServiceResponse> FollowShopAsync(Guid userId, Guid shopId)
    {
        try
        {
            var shopExists = await _context.Shops.AnyAsync(s => s.Id == shopId && s.Status == 1);
            if (!shopExists)
                return new ServiceResponse { Success = false, Message = "Shop không tồn tại" };

            var alreadyFollowing = await _context.ShopFollows
                .AnyAsync(f => f.UserId == userId && f.ShopId == shopId);

            if (alreadyFollowing)
                return new ServiceResponse { Success = true, Message = "Đã theo dõi shop này rồi" };

            _context.ShopFollows.Add(new ShopFollow
            {
                UserId = userId,
                ShopId = shopId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return new ServiceResponse { Success = true, Message = "Theo dõi shop thành công" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error following shop {ShopId}", shopId);
            return new ServiceResponse { Success = false, Message = "Có lỗi xảy ra" };
        }
    }

    public async Task<ServiceResponse> UnfollowShopAsync(Guid userId, Guid shopId)
    {
        try
        {
            var follow = await _context.ShopFollows
                .FirstOrDefaultAsync(f => f.UserId == userId && f.ShopId == shopId);

            if (follow is null)
                return new ServiceResponse { Success = true, Message = "Chưa theo dõi shop này" };

            _context.ShopFollows.Remove(follow);
            await _context.SaveChangesAsync();
            return new ServiceResponse { Success = true, Message = "Hủy theo dõi thành công" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unfollowing shop {ShopId}", shopId);
            return new ServiceResponse { Success = false, Message = "Có lỗi xảy ra" };
        }
    }
}
