using ECommerceAPI.Application.DTOs.Storefront;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class CategoryStorefrontService : ICategoryStorefrontService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CategoryStorefrontService> _logger;

    public CategoryStorefrontService(
        ApplicationDbContext context,
        ILogger<CategoryStorefrontService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CategoryStorefrontListResponseDto> GetCategoriesAsync(
        int page,
        int pageSize,
        short? level = null)
    {
        try
        {
            var query = _context.Categories
                .Where(c => c.IsActive)
                .AsQueryable();

            if (level.HasValue)
                query = query.Where(c => c.Level == level.Value);

            var totalCount = await query.CountAsync();

            var categories = await query
                .OrderBy(c => c.Level)
                .ThenBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CategoryStorefrontDto
                {
                    Id = c.Id,
                    ParentId = c.ParentId,
                    Code = c.Code,
                    Name = c.Name,
                    Slug = c.Slug,
                    Level = c.Level,
                    ProductCount = 0,
                    Image = c.Image
                })
                .ToListAsync();

            var subtreeCounts = await BuildSubtreeStorefrontProductCountsAsync();
            foreach (var dto in categories)
                dto.ProductCount = subtreeCounts.GetValueOrDefault(dto.Id, 0);

            return new CategoryStorefrontListResponseDto
            {
                Success = true,
                Categories = categories,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching storefront categories");
            return new CategoryStorefrontListResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy danh sách danh mục"
            };
        }
    }

    public async Task<CategoryStorefrontTreeResponseDto> GetCategoryTreeAsync()
    {
        try
        {
            var allCategories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Level)
                .ThenBy(c => c.Name)
                .Select(c => new CategoryStorefrontDto
                {
                    Id = c.Id,
                    ParentId = c.ParentId,
                    Code = c.Code,
                    Name = c.Name,
                    Slug = c.Slug,
                    Level = c.Level,
                    ProductCount = 0,
                    Image = c.Image
                })
                .ToListAsync();

            var subtreeCounts = await BuildSubtreeStorefrontProductCountsAsync();
            foreach (var dto in allCategories)
                dto.ProductCount = subtreeCounts.GetValueOrDefault(dto.Id, 0);

            var tree = BuildTree(allCategories, null);

            return new CategoryStorefrontTreeResponseDto { Success = true, Tree = tree };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching storefront category tree");
            return new CategoryStorefrontTreeResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy cây danh mục"
            };
        }
    }

    public async Task<CategoryStorefrontDetailResponseDto> GetCategoryByIdAsync(long categoryId)
    {
        try
        {
            var category = await _context.Categories
                .Where(c => c.Id == categoryId && c.IsActive)
                .Select(c => new CategoryStorefrontDto
                {
                    Id = c.Id,
                    ParentId = c.ParentId,
                    Code = c.Code,
                    Name = c.Name,
                    Slug = c.Slug,
                    Level = c.Level,
                    ProductCount = 0,
                    Image = c.Image,
                    Subcategories = c.InverseParent
                        .Where(sub => sub.IsActive)
                        .Select(sub => new CategoryStorefrontDto
                        {
                            Id = sub.Id,
                            ParentId = sub.ParentId,
                            Code = sub.Code,
                            Name = sub.Name,
                            Slug = sub.Slug,
                            Level = sub.Level,
                            ProductCount = 0,
                            Image = sub.Image
                        })
                        .OrderBy(sub => sub.Name)
                        .ToList(),
                })
                .FirstOrDefaultAsync();

            if (category is null)
                return new CategoryStorefrontDetailResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy danh mục"
                };

            var subtreeCounts = await BuildSubtreeStorefrontProductCountsAsync();
            category.ProductCount = subtreeCounts.GetValueOrDefault(category.Id, 0);
            foreach (var sub in category.Subcategories)
                sub.ProductCount = subtreeCounts.GetValueOrDefault(sub.Id, 0);

            return new CategoryStorefrontDetailResponseDto { Success = true, Category = category };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching storefront category {CategoryId}", categoryId);
            return new CategoryStorefrontDetailResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy thông tin danh mục"
            };
        }
    }

    private static List<CategoryStorefrontDto> BuildTree(
        List<CategoryStorefrontDto> all,
        long? parentId)
    {
        return all
            .Where(c => c.ParentId == parentId)
            .Select(c =>
            {
                c.Subcategories = BuildTree(all, c.Id);
                return c;
            })
            .ToList();
    }

    /// <summary>
    /// Tổng sản phẩm hiển thị được trên storefront (như filter /api/products) trong từng danh mục
    /// và toàn bộ danh mục con — khớp với <see cref="ProductStorefrontService.GetProductsAsync"/>.
    /// </summary>
    private async Task<Dictionary<long, int>> BuildSubtreeStorefrontProductCountsAsync()
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.ParentId })
            .ToListAsync();

        var directRows = await _context.Products
            .AsNoTracking()
            .Where(p =>
                p.Status == (short)ProductStatus.Active
                && p.Shop != null
                && p.Shop.Status == 1
                && p.Shop.VerificationStatus == 1
                && p.CategoryId.HasValue)
            .GroupBy(p => p.CategoryId!.Value)
            .Select(g => new { CatId = g.Key, Cnt = g.Count() })
            .ToListAsync();

        var directMap = directRows.ToDictionary(x => x.CatId, x => x.Cnt);

        var childrenByParent = new Dictionary<long, List<long>>();
        foreach (var c in categories)
        {
            if (!c.ParentId.HasValue)
                continue;

            if (!childrenByParent.TryGetValue(c.ParentId.Value, out var list))
            {
                list = [];
                childrenByParent[c.ParentId.Value] = list;
            }

            list.Add(c.Id);
        }

        var subtree = new Dictionary<long, int>();

        int SumSubtree(long id)
        {
            if (subtree.TryGetValue(id, out var memo))
                return memo;

            var sum = directMap.GetValueOrDefault(id, 0);
            if (childrenByParent.TryGetValue(id, out var kids))
            {
                foreach (var childId in kids)
                    sum += SumSubtree(childId);
            }

            subtree[id] = sum;
            return sum;
        }

        foreach (var c in categories)
            SumSubtree(c.Id);

        return subtree;
    }
}
