using ECommerceAPI.Application.DTOs.Reviews;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _context;

    public ReviewService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResponse<ProductReviewDto>> CreateProductReviewAsync(Guid userId, CreateProductReviewDto dto)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.CustomerId == userId);

        if (order == null)
        {
            return new ServiceResponse<ProductReviewDto>
            {
                Success = false,
                Message = "Không tìm thấy đơn hàng"
            };
        }

        if ((OrderStatus)order.Status != OrderStatus.Completed)
        {
            return new ServiceResponse<ProductReviewDto>
            {
                Success = false,
                Message = "Chỉ có thể đánh giá đơn hàng đã hoàn thành"
            };
        }

        var hasProductInOrder = order.OrderItems.Any(oi => oi.ProductId == dto.ProductId);
        if (!hasProductInOrder)
        {
            return new ServiceResponse<ProductReviewDto>
            {
                Success = false,
                Message = "Sản phẩm không thuộc đơn hàng này"
            };
        }

        var existing = await _context.ProductReviews
            .FirstOrDefaultAsync(r => r.ProductId == dto.ProductId && r.UserId == userId);

        if (existing != null)
        {
            return new ServiceResponse<ProductReviewDto>
            {
                Success = false,
                Message = "Bạn đã đánh giá sản phẩm này"
            };
        }

        var review = new ProductReview
        {
            Id = Guid.NewGuid(),
            ProductId = dto.ProductId,
            UserId = userId,
            Rating = dto.Rating,
            Content = dto.Comment,
            Status = (short)ReviewStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ProductReviews.Add(review);

        await _context.SaveChangesAsync();

        var dtoResult = await _context.ProductReviews
            .Include(r => r.User)
            .Where(r => r.Id == review.Id)
            .Select(r => new ProductReviewDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                UserId = r.UserId,
                UserName = r.User.FullName,
                Rating = r.Rating,
                Comment = r.Content,
                CreatedAt = r.CreatedAt,
                ImageUrls = new List<string>()
            })
            .FirstAsync();

        return new ServiceResponse<ProductReviewDto>
        {
            Success = true,
            Message = "Tạo đánh giá thành công",
            Data = dtoResult
        };
    }

    public async Task<ProductReviewListResponseDto> GetProductReviewsAsync(
        Guid productId,
        int page,
        int pageSize,
        string? sortBy = null)
    {
        var query = _context.ProductReviews
            .Include(r => r.User)
            .Where(r => r.ProductId == productId && r.Status == (short)ReviewStatus.Approved);

        query = sortBy switch
        {
            "rating" => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            "rating_asc" => query.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        var reviews = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ProductReviewDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                UserId = r.UserId,
                UserName = r.User.FullName,
                Rating = r.Rating,
                Comment = r.Content,
                CreatedAt = r.CreatedAt,
                ImageUrls = new List<string>()
            })
            .ToListAsync();

        return new ProductReviewListResponseDto
        {
            Success = true,
            Reviews = reviews,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ServiceResponse<ShopReviewDto>> CreateShopReviewAsync(Guid userId, CreateShopReviewDto dto)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.CustomerId == userId);

        if (order == null)
            return new ServiceResponse<ShopReviewDto> { Success = false, Message = "Không tìm thấy đơn hàng" };

        if ((OrderStatus)order.Status != OrderStatus.Completed)
            return new ServiceResponse<ShopReviewDto> { Success = false, Message = "Chỉ có thể đánh giá shop sau khi đơn hàng hoàn thành" };

        var shopExists = await _context.Shops.AnyAsync(s => s.Id == dto.ShopId);
        if (!shopExists)
            return new ServiceResponse<ShopReviewDto> { Success = false, Message = "Không tìm thấy shop" };

        if (order.ShopId != dto.ShopId)
            return new ServiceResponse<ShopReviewDto> { Success = false, Message = "Đơn hàng này không thuộc shop được đánh giá" };

        var existing = await _context.ShopReviews
            .FirstOrDefaultAsync(r => r.ShopId == dto.ShopId && r.UserId == userId && r.OrderId == dto.OrderId);

        if (existing != null)
            return new ServiceResponse<ShopReviewDto> { Success = false, Message = "Bạn đã đánh giá shop này cho đơn hàng này rồi" };

        var review = new Domain.Entities.ShopReview
        {
            Id = Guid.NewGuid(),
            ShopId = dto.ShopId,
            UserId = userId,
            OrderId = dto.OrderId,
            Rating = dto.Rating,
            Title = dto.Title,
            Content = dto.Content,
            Status = (short)ReviewStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ShopReviews.Add(review);
        await _context.SaveChangesAsync();

        var result = await _context.ShopReviews
            .Include(r => r.User)
            .Where(r => r.Id == review.Id)
            .Select(r => new ShopReviewDto
            {
                Id = r.Id,
                ShopId = r.ShopId,
                UserId = r.UserId,
                UserName = r.User.FullName,
                Rating = r.Rating,
                Title = r.Title,
                Content = r.Content,
                CreatedAt = r.CreatedAt
            })
            .FirstAsync();

        return new ServiceResponse<ShopReviewDto> { Success = true, Message = "Đánh giá shop thành công", Data = result };
    }

    public async Task<ShopReviewListResponseDto> GetShopReviewsAsync(
        Guid shopId,
        int page,
        int pageSize,
        string? sortBy = null)
    {
        var query = _context.ShopReviews
            .Include(r => r.User)
            .Where(r => r.ShopId == shopId && r.Status == (short)ReviewStatus.Approved);

        query = sortBy switch
        {
            "rating" => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            "rating_asc" => query.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var averageRating = totalCount > 0 ? await query.AverageAsync(r => (double)r.Rating) : 0;

        var reviews = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ShopReviewDto
            {
                Id = r.Id,
                ShopId = r.ShopId,
                UserId = r.UserId,
                UserName = r.User.FullName,
                Rating = r.Rating,
                Title = r.Title,
                Content = r.Content,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return new ShopReviewListResponseDto
        {
            Success = true,
            Reviews = reviews,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            AverageRating = Math.Round(averageRating, 1)
        };
    }
}

