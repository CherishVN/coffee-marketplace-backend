using System.Collections.Generic;
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
            ImageUrls = NormalizeReviewImages(dto.ImageUrls),
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
                ImageUrls = r.ImageUrls,
                SellerReply = r.SellerReply,
                HelpfulCount = 0
            })
            .FirstAsync();

        return new ServiceResponse<ProductReviewDto>
        {
            Success = true,
            Message = "Tạo đánh giá thành công",
            Data = dtoResult
        };
    }

    public async Task<ProductReviewStatsResponseDto> GetProductReviewStatsAsync(Guid productId)
    {
        var approved = (short)ReviewStatus.Approved;
        var baseQuery = _context.ProductReviews.AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == approved);

        var total = await baseQuery.CountAsync();

        var groups = await baseQuery
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Cnt = g.Count() })
            .ToListAsync();

        var c1 = 0;
        var c2 = 0;
        var c3 = 0;
        var c4 = 0;
        var c5 = 0;
        foreach (var g in groups)
        {
            switch (g.Rating)
            {
                case 1: c1 = g.Cnt; break;
                case 2: c2 = g.Cnt; break;
                case 3: c3 = g.Cnt; break;
                case 4: c4 = g.Cnt; break;
                case 5: c5 = g.Cnt; break;
            }
        }

        var withComment = await baseQuery.CountAsync(r =>
            r.Content != null && r.Content.Trim().Length > 0);

        var imageLists = await baseQuery
            .Select(r => r.ImageUrls)
            .ToListAsync();
        var withImage = imageLists.Count(urls => urls.Count > 0);

        return new ProductReviewStatsResponseDto
        {
            Success = true,
            Data = new ProductReviewStatsDto
            {
                Total = total,
                Count1 = c1,
                Count2 = c2,
                Count3 = c3,
                Count4 = c4,
                Count5 = c5,
                WithComment = withComment,
                WithImage = withImage
            }
        };
    }

    public async Task<ProductReviewListResponseDto> GetProductReviewsAsync(
        Guid productId,
        int page,
        int pageSize,
        string? sortBy = null,
        short? rating = null,
        bool? hasComment = null,
        bool? hasImage = null)
    {
        var approved = (short)ReviewStatus.Approved;
        var query = _context.ProductReviews
            .Where(r => r.ProductId == productId && r.Status == approved);

        if (rating is >= 1 and <= 5)
            query = query.Where(r => r.Rating == rating);

        if (hasComment == true)
            query = query.Where(r => r.Content != null && r.Content.Trim().Length > 0);

        if (hasImage == true)
        {
            // image_urls là text JSON — dùng EF.Property<string> để truy cập raw value,
            // so sánh với "[]" (mảng rỗng) để lọc review có ảnh.
            query = query.Where(r =>
                EF.Property<string>(r, "ImageUrls") != null &&
                EF.Property<string>(r, "ImageUrls") != "[]");
        }

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
                ImageUrls = r.ImageUrls,
                SellerReply = r.SellerReply,
                HelpfulCount = 0
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

    public Task<ServiceResponse<ShopReviewDto>> CreateShopReviewAsync(Guid userId, CreateShopReviewDto dto)
    {
        return Task.FromResult(new ServiceResponse<ShopReviewDto> { Success = false, Message = "Tính năng đánh giá shop đã bị vô hiệu hóa." });
    }

    public async Task<ShopReviewListResponseDto> GetShopReviewsAsync(
        Guid shopId,
        int page,
        int pageSize,
        string? sortBy = null)
    {
        var query = _context.ProductReviews
            .Include(r => r.User)
            .Include(r => r.Product)
            .Where(r => r.Product.ShopId == shopId && r.Status == (short)ReviewStatus.Approved);

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
                ShopId = r.Product.ShopId,
                UserId = r.UserId,
                UserName = r.User.FullName,
                Rating = r.Rating,
                Title = r.Product.Name,
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

    /// <summary>Tối đa 5 ảnh; cắt chuỗi quá dài (data URL/base64).</summary>
    private static List<string> NormalizeReviewImages(IReadOnlyList<string>? urls)
    {
        if (urls == null || urls.Count == 0)
            return new List<string>();

        const int maxImages = 5;
        const int maxCharsPerImage = 600_000;

        var result = new List<string>();
        foreach (var raw in urls.Take(maxImages))
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var s = raw.Trim();
            if (s.Length > maxCharsPerImage)
                s = s[..maxCharsPerImage];
            result.Add(s);
        }

        return result;
    }
}

