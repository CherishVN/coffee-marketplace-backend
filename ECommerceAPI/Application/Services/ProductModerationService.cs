using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class ProductModerationService : IProductModerationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductModerationService> _logger;

    public ProductModerationService(
        ApplicationDbContext context,
        ILogger<ProductModerationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProductModerationListResponseDto> GetAllProductsAsync(
        int page, 
        int pageSize, 
        short? status = null,
        Guid? shopId = null,
        string? search = null)
    {
        try
        {
            var query = _context.Products
                .Include(p => p.Shop)
                .Include(p => p.Category)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            if (shopId.HasValue)
            {
                query = query.Where(p => p.ShopId == shopId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Name.Contains(search));
            }

            var totalCount = await query.CountAsync();

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductModerationDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    ShopId = p.ShopId,
                    ShopName = p.Shop.Name,
                    Status = p.Status,
                    StatusName = ((ProductStatus)p.Status).ToString(),
                    BasePrice = p.BasePrice,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    ImageUrls = p.ProductImages
                        .OrderBy(img => img.SortOrder)
                        .Select(img => img.ImageUrl)
                        .ToList()
                })
                .ToListAsync();

            return new ProductModerationListResponseDto
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
            _logger.LogError(ex, "Error getting products for moderation");
            return new ProductModerationListResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy danh sách sản phẩm"
            };
        }
    }

    public async Task<ProductModerationResponseDto> GetProductByIdAsync(Guid productId)
    {
        try
        {
            var product = await _context.Products
                .Include(p => p.Shop)
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Where(p => p.Id == productId)
                .Select(p => new ProductModerationDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    ShopId = p.ShopId,
                    ShopName = p.Shop.Name,
                    Status = p.Status,
                    StatusName = ((ProductStatus)p.Status).ToString(),
                    BasePrice = p.BasePrice,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    ImageUrls = p.ProductImages
                        .OrderBy(img => img.SortOrder)
                        .Select(img => img.ImageUrl)
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };
            }

            return new ProductModerationResponseDto
            {
                Success = true,
                Product = product
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product: {ProductId}", productId);
            return new ProductModerationResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy thông tin sản phẩm"
            };
        }
    }

    public async Task<ProductModerationResponseDto> HideProductAsync(
        Guid productId, 
        HideProductDto dto, 
        Guid adminId)
    {
        try
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };
            }

            product.Status = (short)ProductStatus.Hidden;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Product hidden: {ProductId} by admin: {AdminId}. Reason: {Reason}", 
                productId, adminId, dto.Reason);

            return new ProductModerationResponseDto
            {
                Success = true,
                Message = "Ẩn sản phẩm thành công"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hiding product: {ProductId}", productId);
            return new ProductModerationResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi ẩn sản phẩm"
            };
        }
    }

    public async Task<ProductModerationResponseDto> UnhideProductAsync(Guid productId, Guid adminId)
    {
        try
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };
            }

            product.Status = (short)ProductStatus.Active;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Product unhidden: {ProductId} by admin: {AdminId}", productId, adminId);

            return new ProductModerationResponseDto
            {
                Success = true,
                Message = "Hiển thị lại sản phẩm thành công"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unhiding product: {ProductId}", productId);
            return new ProductModerationResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi hiển thị lại sản phẩm"
            };
        }
    }

    public async Task<ProductModerationResponseDto> RemoveProductAsync(
        Guid productId, 
        RemoveProductDto dto, 
        Guid adminId)
    {
        try
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                return new ProductModerationResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy sản phẩm"
                };
            }

            product.Status = (short)ProductStatus.Removed;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Product removed: {ProductId} by admin: {AdminId}. Reason: {Reason}", 
                productId, adminId, dto.Reason);

            return new ProductModerationResponseDto
            {
                Success = true,
                Message = "Gỡ sản phẩm thành công"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing product: {ProductId}", productId);
            return new ProductModerationResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi gỡ sản phẩm"
            };
        }
    }
}
