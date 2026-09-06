using System.Text.Json;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public static class ProductApprovedSnapshotBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static ProductApprovedSnapshotDto ToDto(Product p)
    {
        var imageUrls = p.ProductImages
            .OrderBy(i => i.SortOrder)
            .Select(i => i.ImageUrl)
            .ToList();

        var tagIds = p.ProductTags
            .OrderBy(t => t.TagId)
            .Select(t => t.TagId)
            .ToList();
        var tagNames = p.ProductTags
            .OrderBy(t => t.TagId)
            .Select(t => t.Tag.Name)
            .ToList();

        var materialIds = p.ProductMaterials
            .OrderBy(m => m.MaterialId)
            .Select(m => m.MaterialId)
            .ToList();
        var materialNames = p.ProductMaterials
            .OrderBy(m => m.MaterialId)
            .Select(m => m.Material.Name)
            .ToList();

        int? baseQty = null;
        if (!p.ProductVariants.Any())
        {
            var inv = p.Inventories.FirstOrDefault(i => i.VariantId == null);
            baseQty = inv?.Quantity;
        }

        var variants = new List<ProductApprovedVariantSnapshotDto>();
        foreach (var v in p.ProductVariants.OrderBy(x => x.CreatedAt))
        {
            var inv = p.Inventories.FirstOrDefault(i => i.VariantId == v.Id);
            variants.Add(new ProductApprovedVariantSnapshotDto
            {
                VariantName = v.VariantName,
                Sku = v.Sku,
                Price = v.Price,
                Stock = inv?.Quantity ?? 0,
                Attributes = v.Attributes,
            });
        }

        return new ProductApprovedSnapshotDto
        {
            CapturedAtUtc = DateTime.UtcNow.ToString("O"),
            Name = p.Name,
            Description = p.Description,
            BasePrice = p.BasePrice,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name,
            ImageUrls = imageUrls,
            TagIds = tagIds,
            TagNames = tagNames,
            MaterialIds = materialIds,
            MaterialNames = materialNames,
            BaseInventoryQuantity = baseQty,
            Variants = variants,
        };
    }

    public static string SerializeToJson(Product p)
    {
        var dto = ToDto(p);
        return JsonSerializer.Serialize(dto, JsonOpts);
    }

    public static async Task UpdateLastApprovedJsonAsync(
        ECommerceAPI.Infrastructure.Data.ApplicationDbContext context,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var p = await context.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.ProductImages)
            .Include(x => x.ProductTags).ThenInclude(pt => pt.Tag)
            .Include(x => x.ProductMaterials).ThenInclude(pm => pm.Material)
            .Include(x => x.ProductVariants)
            .Include(x => x.Inventories)
            .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);

        if (p == null) return;
        var json = SerializeToJson(p);
        await context.Products
            .Where(x => x.Id == productId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.LastApprovedSnapshotJson, json),
                cancellationToken);
    }
}
