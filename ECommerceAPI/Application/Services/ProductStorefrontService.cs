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
        double? minRating = null,
        string? sortBy = null,
        List<long>? tagIds = null,
        List<Guid>? materialIds = null)
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
            {
                var allowedCategoryIds = await GetDescendantCategoryIdsAsync(categoryId.Value);
                query = query.Where(p => p.CategoryId.HasValue && allowedCategoryIds.Contains(p.CategoryId.Value));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim();
                var searchLower = searchTerm.ToLower();

                query = query.Where(p =>
                    (p.SearchVector != null && p.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("simple", searchTerm)))
                    || p.Name.ToLower().Contains(searchLower)
                    || (p.Description != null && p.Description.ToLower().Contains(searchLower)));
            }

            // Giá trên danh sách = min(giá gốc, variant active); variant không Price thì dùng giá gốc.
            if (minPrice.HasValue)
            {
                query = query.Where(p =>
                    (!p.ProductVariants.Any(v => v.IsActive)
                        ? p.BasePrice
                        : Math.Min(
                            p.BasePrice,
                            p.ProductVariants.Where(v => v.IsActive).Min(v => v.Price ?? p.BasePrice)))
                    >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p =>
                    (!p.ProductVariants.Any(v => v.IsActive)
                        ? p.BasePrice
                        : Math.Min(
                            p.BasePrice,
                            p.ProductVariants.Where(v => v.IsActive).Min(v => v.Price ?? p.BasePrice)))
                    <= maxPrice.Value);
            }

            if (minRating.HasValue)
                query = query.Where(p => p.ProductReviews.Any() && p.ProductReviews.Average(r => (double)r.Rating) >= minRating.Value);

            if (tagIds != null && tagIds.Count > 0)
                query = query.Where(p => p.ProductTags.Any(pt => tagIds.Contains(pt.TagId)));

            if (materialIds != null && materialIds.Count > 0)
                query = query.Where(p => p.ProductMaterials.Any(pm => materialIds.Contains(pm.MaterialId)));

            query = sortBy switch
            {
                "price_asc" => query.OrderBy(p =>
                        !p.ProductVariants.Any(v => v.IsActive)
                            ? p.BasePrice
                            : Math.Min(
                                p.BasePrice,
                                p.ProductVariants.Where(v => v.IsActive).Min(v => v.Price ?? p.BasePrice)))
                    .ThenBy(p => p.Id),
                "price_desc" => query.OrderByDescending(p =>
                        !p.ProductVariants.Any(v => v.IsActive)
                            ? p.BasePrice
                            : Math.Min(
                                p.BasePrice,
                                p.ProductVariants.Where(v => v.IsActive).Min(v => v.Price ?? p.BasePrice)))
                    .ThenBy(p => p.Id),
                "rating"      => query.OrderByDescending(p => p.ProductReviews.Any() ? p.ProductReviews.Average(r => (double)r.Rating) : 0).ThenBy(p => p.Id),
                "newest"      => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
                "best_seller" => query.OrderByDescending(p => p.SoldCount).ThenBy(p => p.Id),
                _             => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            };

            var totalCount = await query.CountAsync();

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductStorefrontDto
                {
                    Id           = p.Id,
                    Slug         = p.Slug,
                    Name         = p.Name,
                    ShopId       = p.ShopId,
                    ShopName     = p.Shop.Name,
                    ShopSlug     = p.Shop.Slug,
                    BasePrice    = !p.ProductVariants.Any(v => v.IsActive)
                        ? p.BasePrice
                        : Math.Min(
                            p.BasePrice,
                            p.ProductVariants.Where(v => v.IsActive).Min(v => v.Price ?? p.BasePrice)),
                    Currency     = p.Currency,
                    CategoryId   = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategorySlug = p.Category != null ? p.Category.Slug : null,
                    ImageUrls    = p.ProductImages
                        .OrderBy(img => img.SortOrder)
                        .Select(img => img.ImageUrl)
                        .ToList(),
                    CreatedAt    = p.CreatedAt,
                    SoldCount    = p.SoldCount,
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
                .Include(p => p.ProductVariants).ThenInclude(v => v.Inventories)
                .Include(p => p.Inventories)
                .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.ProductMaterials).ThenInclude(pm => pm.Material)
                .Where(p => p.Id == productId && p.Status == (short)ProductStatus.Active)
                .Select(p => new ProductStorefrontDetailDto
                {
                    Id           = p.Id,
                    Slug         = p.Slug,
                    Name         = p.Name,
                    Description  = p.Description,
                    ShopId       = p.ShopId,
                    ShopName     = p.Shop.Name,
                    ShopSlug     = p.Shop.Slug,
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
                            Id            = v.Id,
                            VariantName   = v.VariantName,
                            Attributes    = v.Attributes,
                            Price         = v.Price,
                            IsActive      = v.IsActive,
                            StockQuantity = v.Inventories
                                .Sum(i => Math.Max(0, i.Quantity - i.ReservedQuantity)),
                        })
                        .ToList(),
                    TotalStock = p.Inventories
                        .Sum(i => Math.Max(0, i.Quantity - i.ReservedQuantity)),
                    Tags = p.ProductTags.Select(pt => pt.Tag.Name).ToList(),
                    Materials = p.ProductMaterials.Select(pm => pm.Material.Name).ToList(),
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

    public async Task<ProductStorefrontListResponseDto> GetSuggestionsAsync(int limit = 10)
    {
        try
        {
            // Top bán chạy + mới nhất, trộn để đa dạng
            var trending = await _context.Products
                .Include(p => p.Shop)
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Where(p => p.Status == (short)ProductStatus.Active && p.SoldCount > 0)
                .OrderByDescending(p => p.SoldCount)
                .Take(limit / 2)
                .Select(p => new ProductStorefrontDto
                {
                    Id = p.Id, Slug = p.Slug, Name = p.Name,
                    ShopId = p.ShopId, ShopName = p.Shop.Name, ShopSlug = p.Shop.Slug,
                    BasePrice = !p.ProductVariants.Any(v => v.IsActive)
                        ? p.BasePrice
                        : Math.Min(
                            p.BasePrice,
                            p.ProductVariants.Where(v => v.IsActive).Min(v => v.Price ?? p.BasePrice)),
                    Currency = p.Currency,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategorySlug = p.Category != null ? p.Category.Slug : null,
                    ImageUrls = p.ProductImages.OrderBy(img => img.SortOrder).Select(img => img.ImageUrl).ToList(),
                    CreatedAt = p.CreatedAt, SoldCount = p.SoldCount
                })
                .ToListAsync();

            var newest = await _context.Products
                .Include(p => p.Shop)
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Where(p => p.Status == (short)ProductStatus.Active && trending.Select(t => t.Id).All(id => id != p.Id))
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit - trending.Count)
                .Select(p => new ProductStorefrontDto
                {
                    Id = p.Id, Slug = p.Slug, Name = p.Name,
                    ShopId = p.ShopId, ShopName = p.Shop.Name, ShopSlug = p.Shop.Slug,
                    BasePrice = !p.ProductVariants.Any(v => v.IsActive)
                        ? p.BasePrice
                        : Math.Min(
                            p.BasePrice,
                            p.ProductVariants.Where(v => v.IsActive).Min(v => v.Price ?? p.BasePrice)),
                    Currency = p.Currency,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategorySlug = p.Category != null ? p.Category.Slug : null,
                    ImageUrls = p.ProductImages.OrderBy(img => img.SortOrder).Select(img => img.ImageUrl).ToList(),
                    CreatedAt = p.CreatedAt, SoldCount = p.SoldCount
                })
                .ToListAsync();

            var combined = trending.Concat(newest).ToList();

            return new ProductStorefrontListResponseDto
            {
                Success = true,
                Products = combined,
                TotalCount = combined.Count,
                Page = 1,
                PageSize = limit
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product suggestions");
            return new ProductStorefrontListResponseDto { Success = false, Message = "Có lỗi xảy ra khi lấy gợi ý sản phẩm" };
        }
    }

    public async Task<ProductStorefrontDetailResponseDto> GetProductBySlugAsync(string slug)
    {
        try
        {
            var product = await _context.Products
                .Include(p => p.Shop)
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductReviews)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Inventories)
                .Include(p => p.Inventories)
                .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.ProductMaterials).ThenInclude(pm => pm.Material)
                .Where(p => p.Slug == slug && p.Status == (short)ProductStatus.Active)
                .Select(p => new ProductStorefrontDetailDto
                {
                    Id           = p.Id,
                    Slug         = p.Slug,
                    Name         = p.Name,
                    Description  = p.Description,
                    ShopId       = p.ShopId,
                    ShopName     = p.Shop.Name,
                    ShopSlug     = p.Shop.Slug,
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
                            Id            = v.Id,
                            VariantName   = v.VariantName,
                            Attributes    = v.Attributes,
                            Price         = v.Price,
                            IsActive      = v.IsActive,
                            StockQuantity = v.Inventories
                                .Sum(i => Math.Max(0, i.Quantity - i.ReservedQuantity)),
                        })
                        .ToList(),
                    TotalStock = p.Inventories
                        .Sum(i => Math.Max(0, i.Quantity - i.ReservedQuantity)),
                    Tags = p.ProductTags.Select(pt => pt.Tag.Name).ToList(),
                    Materials = p.ProductMaterials.Select(pm => pm.Material.Name).ToList(),
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
            _logger.LogError(ex, "Error fetching storefront product by slug {Slug}", slug);
            return new ProductStorefrontDetailResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy thông tin sản phẩm"
            };
        }
    }

    private async Task<HashSet<long>> GetDescendantCategoryIdsAsync(long rootCategoryId)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.ParentId })
            .ToListAsync();

        var childrenByParent = categories
            .Where(c => c.ParentId.HasValue)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var result = new HashSet<long> { rootCategoryId };
        var queue = new Queue<long>();
        queue.Enqueue(rootCategoryId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var childId in children)
            {
                if (!result.Add(childId))
                {
                    continue;
                }

                queue.Enqueue(childId);
            }
        }

        return result;
    }
}
