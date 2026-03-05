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
                    ProductCount = c.Products.Count(p => p.Status == (short)ProductStatus.Active),
                })
                .ToListAsync();

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
                    ProductCount = c.Products.Count(p => p.Status == (short)ProductStatus.Active),
                })
                .ToListAsync();

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
                    ProductCount = c.Products.Count(p => p.Status == (short)ProductStatus.Active),
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
                            ProductCount = sub.Products.Count(p => p.Status == (short)ProductStatus.Active),
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
}
