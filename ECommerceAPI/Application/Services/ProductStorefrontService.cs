using ECommerceAPI.Application.DTOs.Storefront;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class ProductStorefrontService : IProductStorefrontService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductStorefrontService> _logger;

    public ProductStorefrontService(
        ApplicationDbContext context,
        ILogger<ProductStorefrontService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductStorefrontListResponseDto> GetProductsAsync(
        int page,
        int pageSize,
        long? categoryId = null,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sortBy = null)
    {
        try
        {
            var query = _context.Products
                .Include(p => p.Shop)
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Where(p => p.Status == (short)ProductStatus.Active)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(searchLower)
                    || (p.Description != null && p.Description.ToLower().Contains(searchLower)));
            }

            if (minPrice.HasValue)
                query = query.Where(p => p.BasePrice >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.BasePrice <= maxPrice.Value);

            query = sortBy switch
            {
                "price_asc"  => query.OrderBy(p => p.BasePrice).ThenBy(p => p.Id),
                "price_desc" => query.OrderByDescending(p => p.BasePrice).ThenBy(p => p.Id),
                "newest"     => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
                _            => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            };

            var totalCount = await query.CountAsync();

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductStorefrontDto
                {
                    Id          = p.Id,
                    Name        = p.Name,
                    ShopId      = p.ShopId,
                    ShopName    = p.Shop.Name,
                    BasePrice   = p.BasePrice,
                    Currency    = p.Currency,
                    CategoryId   = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategorySlug = p.Category != null ? p.Category.Slug : null,
                    ImageUrls   = p.ProductImages
                        .OrderBy(img => img.SortOrder)
                        .Select(img => img.ImageUrl)
                        .ToList(),
                    CreatedAt   = p.CreatedAt,
                    SoldCount   = p.SoldCount,
                })
                .ToListAsync();

            return new ProductStorefrontListResponseDto
            {
                Success    = true,
                Products   = products,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching storefront products");
            return new ProductStorefrontListResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy danh sách sản phẩm"
            };
        }
    }

    public async Task<ProductStorefrontDetailResponseDto> GetProductByIdAsync(Guid productId)
    {
        try
        {
            var product = await _context.Products
                .Include(p => p.Shop)
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductReviews)
                .Include(p => p.ProductVariants)
                .Where(p => p.Id == productId && p.Status == (short)ProductStatus.Active)
                .Select(p => new ProductStorefrontDetailDto
                {
                    Id           = p.Id,
                    Name         = p.Name,
                    Description  = p.Description,
                    ShopId       = p.ShopId,
                    ShopName     = p.Shop.Name,
                    BasePrice    = p.BasePrice,
                    Currency     = p.Currency,
                    CategoryId   = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategorySlug = p.Category != null ? p.Category.Slug : null,
                    AverageRating = p.ProductReviews.Any()
                        ? Math.Round(p.ProductReviews.Average(r => (double)r.Rating), 1)
                        : 0,
                    ReviewCount  = p.ProductReviews.Count,
                    ImageUrls    = p.ProductImages
                        .OrderBy(img => img.SortOrder)
                        .Select(img => img.ImageUrl)
                        .ToList(),
                    Variants = p.ProductVariants
                        .Where(v => v.IsActive)
                        .Select(v => new ProductVariantStorefrontDto
                        {
                            Id          = v.Id,
                            VariantName = v.VariantName,
                            Price       = v.Price,
                            IsActive    = v.IsActive,
                        })
                        .ToList(),
                    CreatedAt = p.CreatedAt,
                    SoldCount = p.SoldCount,
                })
                .FirstOrDefaultAsync();

            if (product is null)
                return new ProductStorefrontDetailResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };

            return new ProductStorefrontDetailResponseDto { Success = true, Product = product };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching storefront product {ProductId}", productId);
            return new ProductStorefrontDetailResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy thông tin sản phẩm"
            };
        }
    }
}
