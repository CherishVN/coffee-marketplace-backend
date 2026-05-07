using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECommerceAPI.Infrastructure.Data;
using ECommerceAPI.Application.DTOs.Storefront;
using ECommerceAPI.Domain.Enums;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/collections")]
public class CollectionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CollectionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>Returns all active collections with up to 8 preview products each.</summary>
    [HttpGet]
    public async Task<IActionResult> GetCollections()
    {
        var now = DateTime.UtcNow;

        var collMeta = await _context.Collections
            .Where(c => c.IsActive
                     && (c.StartsAt == null || c.StartsAt <= now)
                     && (c.EndsAt   == null || c.EndsAt   >= now))
            .OrderBy(c => c.HomeSortOrder)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id, c.Name, c.Slug,
                c.Description, c.ShortDesc,
                c.Image, c.ImageAlt, c.Type,
                c.CategoryId, c.TagId,
            })
            .ToListAsync();

        if (collMeta.Count == 0)
            return Ok(new { collections = new List<object>() });

        // ── Helper ───────────────────────────────────────────────────────────
        async Task<List<ProductStorefrontDto>> FetchProducts(
            System.Linq.Expressions.Expression<Func<ECommerceAPI.Domain.Entities.Product, bool>> predicate,
            int take = 8)
        {
            return await _context.Products
                .Where(p => p.Status == (short)ProductStatus.Active
                         && p.Shop.Status == 1
                         && p.Shop.VerificationStatus == 1)
                .Where(predicate)
                .OrderByDescending(p => p.SoldCount)
                .ThenByDescending(p => p.CreatedAt)
                .Take(take)
                .Select(p => new ProductStorefrontDto
                {
                    Id            = p.Id,
                    Slug          = p.Slug,
                    Name          = p.Name,
                    ShopId        = p.ShopId,
                    ShopName      = p.Shop.Name,
                    ShopSlug      = p.Shop.Slug ?? string.Empty,
                    ShopLogoUrl   = p.Shop.LogoUrl,
                    BasePrice     = p.BasePrice,
                    Currency      = p.Currency,
                    SoldCount     = p.SoldCount,
                    ImageUrls     = p.ProductImages
                        .OrderBy(img => img.SortOrder)
                        .Select(img => img.ImageUrl)
                        .ToList(),
                    AverageRating = p.ProductReviews.Any()
                        ? p.ProductReviews.Average(r => (double)r.Rating)
                        : 0.0,
                    ReviewCount   = p.ProductReviews.Count,
                })
                .ToListAsync();
        }

        // ── Manual/featured products via junction table ───────────────────────
        var manualIds = collMeta
            .Where(c => c.Type == "manual" || c.Type == "featured" || (c.CategoryId == null && c.TagId == null))
            .Select(c => c.Id)
            .ToList();

        Dictionary<int, List<ProductStorefrontDto>> manualProducts = new();

        if (manualIds.Count > 0)
        {
            var rawManual = await _context.CollectionProducts
                .Where(cp => manualIds.Contains(cp.CollectionId)
                          && cp.Product.Status == (short)ProductStatus.Active
                          && cp.Product.Shop.Status == 1
                          && cp.Product.Shop.VerificationStatus == 1)
                .OrderBy(cp => cp.CollectionId)
                .ThenBy(cp => cp.SortOrder)
                .Select(cp => new
                {
                    cp.CollectionId,
                    Product = new ProductStorefrontDto
                    {
                        Id            = cp.Product.Id,
                        Slug          = cp.Product.Slug,
                        Name          = cp.Product.Name,
                        ShopId        = cp.Product.ShopId,
                        ShopName      = cp.Product.Shop.Name,
                        ShopSlug      = cp.Product.Shop.Slug ?? string.Empty,
                        ShopLogoUrl   = cp.Product.Shop.LogoUrl,
                        BasePrice     = cp.Product.BasePrice,
                        Currency      = cp.Product.Currency,
                        SoldCount     = cp.Product.SoldCount,
                        ImageUrls     = cp.Product.ProductImages
                            .OrderBy(img => img.SortOrder)
                            .Select(img => img.ImageUrl)
                            .ToList(),
                        AverageRating = cp.Product.ProductReviews.Any()
                            ? cp.Product.ProductReviews.Average(r => (double)r.Rating)
                            : 0.0,
                        ReviewCount   = cp.Product.ProductReviews.Count,
                    }
                })
                .ToListAsync();

            manualProducts = rawManual
                .GroupBy(x => x.CollectionId)
                .ToDictionary(g => g.Key, g => g.Take(8).Select(x => x.Product).ToList());
        }

        // ── Assemble ─────────────────────────────────────────────────────────
        List<ProductStorefrontDto>? globalBestSellers = null;
        var collections = new List<CollectionDto>();

        foreach (var c in collMeta)
        {
            List<ProductStorefrontDto> products;

            if (c.Type == "category" && c.CategoryId.HasValue)
            {
                var catId = c.CategoryId.Value;
                products = await FetchProducts(p => p.CategoryId == catId);
            }
            else if (c.Type == "tag" && c.TagId.HasValue)
            {
                var tagId = c.TagId.Value;
                products = await FetchProducts(p => p.ProductTags.Any(pt => pt.TagId == tagId));
            }
            else
            {
                manualProducts.TryGetValue(c.Id, out var mp);
                products = mp ?? new List<ProductStorefrontDto>();
            }

            // Fallback to global best sellers if no products resolved
            if (products.Count == 0)
            {
                globalBestSellers ??= await FetchProducts(p => true);
                products = globalBestSellers;
            }

            var image = c.Image;
            if (string.IsNullOrEmpty(image) && products.Count > 0)
                image = products[0].ImageUrls.FirstOrDefault();

            collections.Add(new CollectionDto
            {
                Id          = c.Id,
                Name        = c.Name,
                Slug        = c.Slug,
                Description = c.Description,
                ShortDesc   = c.ShortDesc,
                Image       = image,
                ImageAlt    = c.ImageAlt ?? c.Name,
                Type        = c.Type,
                Products    = products,
            });
        }

        return Ok(new { collections });
    }

    /// <summary>Returns a single collection by slug with all its products.</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetCollection(string slug)
    {
        var now = DateTime.UtcNow;

        var c = await _context.Collections
            .Where(col => col.Slug == slug
                       && col.IsActive
                       && (col.StartsAt == null || col.StartsAt <= now)
                       && (col.EndsAt   == null || col.EndsAt   >= now))
            .Select(col => new
            {
                col.Id, col.Name, col.Slug,
                col.Description, col.ShortDesc,
                col.Image, col.ImageAlt, col.Type,
                col.CategoryId, col.TagId,
            })
            .FirstOrDefaultAsync();

        if (c == null) return NotFound(new { message = "Không tìm thấy bộ sưu tập" });

        // Fetch products (same logic as list endpoint but unlimited take)
        List<ProductStorefrontDto> products;

        if (c.Type == "category" && c.CategoryId.HasValue)
        {
            var catId = c.CategoryId.Value;
            products = await _context.Products
                .Where(p => p.CategoryId == catId
                         && p.Status == (short)ProductStatus.Active
                         && p.Shop.Status == 1
                         && p.Shop.VerificationStatus == 1)
                .OrderByDescending(p => p.SoldCount)
                .Take(48)
                .Select(p => new ProductStorefrontDto
                {
                    Id = p.Id, Slug = p.Slug, Name = p.Name,
                    ShopId = p.ShopId, ShopName = p.Shop.Name, ShopSlug = p.Shop.Slug ?? "",
                    ShopLogoUrl = p.Shop.LogoUrl, BasePrice = p.BasePrice, Currency = p.Currency,
                    SoldCount = p.SoldCount,
                    ImageUrls = p.ProductImages.OrderBy(img => img.SortOrder).Select(img => img.ImageUrl).ToList(),
                    AverageRating = p.ProductReviews.Any() ? p.ProductReviews.Average(r => (double)r.Rating) : 0,
                    ReviewCount = p.ProductReviews.Count,
                })
                .ToListAsync();
        }
        else if (c.Type == "tag" && c.TagId.HasValue)
        {
            var tagId = c.TagId.Value;
            products = await _context.Products
                .Where(p => p.ProductTags.Any(pt => pt.TagId == tagId)
                         && p.Status == (short)ProductStatus.Active
                         && p.Shop.Status == 1
                         && p.Shop.VerificationStatus == 1)
                .OrderByDescending(p => p.SoldCount)
                .Take(48)
                .Select(p => new ProductStorefrontDto
                {
                    Id = p.Id, Slug = p.Slug, Name = p.Name,
                    ShopId = p.ShopId, ShopName = p.Shop.Name, ShopSlug = p.Shop.Slug ?? "",
                    ShopLogoUrl = p.Shop.LogoUrl, BasePrice = p.BasePrice, Currency = p.Currency,
                    SoldCount = p.SoldCount,
                    ImageUrls = p.ProductImages.OrderBy(img => img.SortOrder).Select(img => img.ImageUrl).ToList(),
                    AverageRating = p.ProductReviews.Any() ? p.ProductReviews.Average(r => (double)r.Rating) : 0,
                    ReviewCount = p.ProductReviews.Count,
                })
                .ToListAsync();
        }
        else
        {
            products = await _context.CollectionProducts
                .Where(cp => cp.CollectionId == c.Id
                          && cp.Product.Status == (short)ProductStatus.Active
                          && cp.Product.Shop.Status == 1
                          && cp.Product.Shop.VerificationStatus == 1)
                .OrderBy(cp => cp.SortOrder)
                .Take(48)
                .Select(cp => new ProductStorefrontDto
                {
                    Id = cp.Product.Id, Slug = cp.Product.Slug, Name = cp.Product.Name,
                    ShopId = cp.Product.ShopId, ShopName = cp.Product.Shop.Name,
                    ShopSlug = cp.Product.Shop.Slug ?? "",
                    ShopLogoUrl = cp.Product.Shop.LogoUrl,
                    BasePrice = cp.Product.BasePrice, Currency = cp.Product.Currency,
                    SoldCount = cp.Product.SoldCount,
                    ImageUrls = cp.Product.ProductImages.OrderBy(img => img.SortOrder).Select(img => img.ImageUrl).ToList(),
                    AverageRating = cp.Product.ProductReviews.Any() ? cp.Product.ProductReviews.Average(r => (double)r.Rating) : 0,
                    ReviewCount = cp.Product.ProductReviews.Count,
                })
                .ToListAsync();
        }

        var image = c.Image ?? products.FirstOrDefault()?.ImageUrls.FirstOrDefault();

        return Ok(new
        {
            collection = new CollectionDto
            {
                Id          = c.Id,
                Name        = c.Name,
                Slug        = c.Slug,
                Description = c.Description,
                ShortDesc   = c.ShortDesc,
                Image       = image,
                ImageAlt    = c.ImageAlt ?? c.Name,
                Type        = c.Type,
                Products    = products,
            }
        });
    }
}
