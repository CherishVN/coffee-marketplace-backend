using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ECommerceAI.Data;
using ECommerceAI.Data.Entities.ReadOnly;
using ECommerceAI.Services;

namespace ECommerceAI.Controllers;

[ApiController]
[Route("api/ai/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly AiDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(AiDbContext context, IMemoryCache cache, ILogger<WebhooksController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public class SupabasePayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = ""; // INSERT, UPDATE, DELETE

        [JsonPropertyName("table")]
        public string Table { get; set; } = "";

        [JsonPropertyName("record")]
        public JsonElement Record { get; set; }

        [JsonPropertyName("old_record")]
        public JsonElement OldRecord { get; set; }
    }

    /// <summary>
    /// Endpoint hứng Database Webhooks từ Supabase (public schema) để đồng bộ dữ liệu sang ai_schema.
    /// URL cài trên Supabase: https://[domain-cua-ban]/api/ai/webhooks/supabase-sync
    /// </summary>
    [HttpPost("supabase-sync")]
    public async Task<IActionResult> HandleSupabaseSync([FromBody] SupabasePayload payload)
    {
        _logger.LogInformation("Received webhook for table {Table}, type {Type}", payload.Table, payload.Type);
        
        var options = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        try
        {
            var table = payload.Table.ToLower();
            var invalidatePromptCatalog = table is "categories" or "tags" or "materials";

            switch (table)
            {
                case "products":
                    await SyncEntity<Product>(payload, options, _context.Products);
                    break;
                case "orders":
                    await SyncEntity<Order>(payload, options, _context.Orders);
                    break;
                case "categories":
                    await SyncEntity<Category>(payload, options, _context.Categories);
                    break;
                case "users":
                    await SyncEntity<AppUser>(payload, options, _context.Users);
                    break;
                case "tags":
                    await SyncEntity<Tag>(payload, options, _context.Tags);
                    break;
                case "materials":
                    await SyncEntity<Material>(payload, options, _context.Materials);
                    break;
                case "shops":
                    await SyncEntity<Shop>(payload, options, _context.Shops);
                    break;
                case "product_variants":
                    await SyncEntity<ProductVariant>(payload, options, _context.ProductVariants);
                    break;
                case "order_items":
                    await SyncEntity<OrderItem>(payload, options, _context.OrderItems);
                    break;
                case "product_images":
                    await SyncEntity<ProductImage>(payload, options, _context.ProductImages);
                    break;
                case "product_tags":
                    await SyncEntity<ProductTag>(payload, options, _context.ProductTags);
                    break;
                case "disputes":
                    await SyncEntity<Dispute>(payload, options, _context.Disputes);
                    break;
                default:
                    _logger.LogInformation("Table {Table} is not mapped for sync, ignoring.", payload.Table);
                    break;
            }

            await _context.SaveChangesAsync();

            if (invalidatePromptCatalog)
            {
                _cache.Remove(PromptCatalogCacheKeys.Candidates);
                _logger.LogInformation("Invalidated prompt catalog cache after webhook on {Table}", payload.Table);
            }

            return Ok(new { success = true, syncedTable = payload.Table });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing webhook for table {Table}", payload.Table);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    private async Task SyncEntity<TEntity>(SupabasePayload payload, JsonSerializerOptions options, DbSet<TEntity> dbSet) 
        where TEntity : class
    {
        if (payload.Type == "DELETE")
        {
            var entity = payload.OldRecord.Deserialize<TEntity>(options);
            if (entity != null)
            {
                // Attach and remove based on primary key
                var entry = _context.Entry(entity);
                entry.State = EntityState.Deleted;
            }
        }
        else // INSERT or UPDATE
        {
            var entity = payload.Record.Deserialize<TEntity>(options);
            if (entity != null)
            {
                var primaryKeyProperty = _context.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey()?.Properties.FirstOrDefault();
                if (primaryKeyProperty != null && primaryKeyProperty.PropertyInfo != null)
                {
                    var keyValue = primaryKeyProperty.PropertyInfo.GetValue(entity);
                    var existing = await dbSet.FindAsync(keyValue);
                    if (existing == null)
                    {
                        dbSet.Add(entity);
                    }
                    else
                    {
                        _context.Entry(existing).CurrentValues.SetValues(entity);
                    }
                }
                else
                {
                    // Fallback for tables with composite keys or no explicit PK info mapped
                    dbSet.Update(entity);
                }
            }
        }
    }

    /// <summary>
    /// Full resync: Gọi Main API lấy lại categories, tags, materials khi AI DB bị mất data.
    /// URL: POST /api/ai/webhooks/full-resync/catalog
    /// </summary>
    [HttpPost("full-resync/catalog")]
    public async Task<IActionResult> ResyncCatalog()
    {
        var httpClientFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient("MainApi");
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };

        try
        {
            // Categories
            var catRes = await client.GetAsync("/api/internal/categories/all");
            if (catRes.IsSuccessStatusCode)
            {
                var categories = JsonSerializer.Deserialize<List<Category>>(await catRes.Content.ReadAsStringAsync(), opts) ?? new();
                foreach (var item in categories)
                {
                    var existing = await _context.Categories.FindAsync(item.Id);
                    if (existing == null) _context.Categories.Add(item);
                    else _context.Entry(existing).CurrentValues.SetValues(item);
                }
            }

            // Tags
            var tagRes = await client.GetAsync("/api/internal/tags/all");
            if (tagRes.IsSuccessStatusCode)
            {
                var tags = JsonSerializer.Deserialize<List<Tag>>(await tagRes.Content.ReadAsStringAsync(), opts) ?? new();
                foreach (var item in tags)
                {
                    var existing = await _context.Tags.FindAsync(item.Id);
                    if (existing == null) _context.Tags.Add(item);
                    else _context.Entry(existing).CurrentValues.SetValues(item);
                }
            }

            // Materials
            var matRes = await client.GetAsync("/api/internal/materials/all");
            if (matRes.IsSuccessStatusCode)
            {
                var materials = JsonSerializer.Deserialize<List<Material>>(await matRes.Content.ReadAsStringAsync(), opts) ?? new();
                foreach (var item in materials)
                {
                    var existing = await _context.Materials.FindAsync(item.Id);
                    if (existing == null) _context.Materials.Add(item);
                    else _context.Entry(existing).CurrentValues.SetValues(item);
                }
            }

            await _context.SaveChangesAsync();
            _cache.Remove(PromptCatalogCacheKeys.Candidates);
            _logger.LogInformation("Full resync catalog completed; prompt catalog cache cleared.");
            return Ok(new { success = true, message = "Đã resync categories, tags, materials từ Main API." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during full resync catalog");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Full resync: Gọi Main API để lấy lại toàn bộ dữ liệu Users và cập nhật vào AI DB.
    /// Dùng khi AI DB bị mất dữ liệu. Chỉ dành cho nội bộ (X-Internal-Key).
    /// URL: POST /api/ai/webhooks/full-resync/users
    /// </summary>
    [HttpPost("full-resync/users")]
    public async Task<IActionResult> ResyncUsers()
    {
        try
        {
            var httpClientFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
            var client = httpClientFactory.CreateClient("MainApi");

            var response = await client.GetAsync("/api/internal/users/all");
            if (!response.IsSuccessStatusCode)
                return StatusCode(502, new { success = false, message = "Không lấy được dữ liệu từ Main API" });

            var json = await response.Content.ReadAsStringAsync();
            var users = JsonSerializer.Deserialize<List<AppUser>>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            });

            if (users == null || users.Count == 0)
                return Ok(new { success = true, message = "Không có dữ liệu để resync" });

            foreach (var user in users)
            {
                var existing = await _context.Users.FindAsync(user.Id);
                if (existing == null)
                    _context.Users.Add(user);
                else
                    _context.Entry(existing).CurrentValues.SetValues(user);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Full resync users: {Count} records synced", users.Count);
            return Ok(new { success = true, syncedCount = users.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during full resync users");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Full resync: Gọi Main API để lấy lại toàn bộ dữ liệu Products + variants + images + tags.
    /// URL: POST /api/ai/webhooks/full-resync/products
    /// </summary>
    [HttpPost("full-resync/products")]
    public async Task<IActionResult> ResyncProducts()
    {
        var httpClientFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient("MainApi");
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        try
        {
            int totalSynced = 0;

            // Products
            var prodRes = await client.GetAsync("/api/internal/products/all");
            if (prodRes.IsSuccessStatusCode)
            {
                var products = JsonSerializer.Deserialize<List<Product>>(await prodRes.Content.ReadAsStringAsync(), opts) ?? new();
                foreach (var item in products)
                {
                    var existing = await _context.Products.FindAsync(item.Id);
                    if (existing == null) _context.Products.Add(item);
                    else _context.Entry(existing).CurrentValues.SetValues(item);
                }
                totalSynced += products.Count;
                _logger.LogInformation("Synced {Count} products", products.Count);
            }

            // Shops (cần cho hiển thị SP)
            var shopRes = await client.GetAsync("/api/internal/shops/all");
            if (shopRes.IsSuccessStatusCode)
            {
                var shops = JsonSerializer.Deserialize<List<Shop>>(await shopRes.Content.ReadAsStringAsync(), opts) ?? new();
                foreach (var item in shops)
                {
                    var existing = await _context.Shops.FindAsync(item.Id);
                    if (existing == null) _context.Shops.Add(item);
                    else _context.Entry(existing).CurrentValues.SetValues(item);
                }
                _logger.LogInformation("Synced {Count} shops", shops.Count);
            }

            // Product Variants
            var varRes = await client.GetAsync("/api/internal/product-variants/all");
            if (varRes.IsSuccessStatusCode)
            {
                var variants = JsonSerializer.Deserialize<List<ProductVariant>>(await varRes.Content.ReadAsStringAsync(), opts) ?? new();
                foreach (var item in variants)
                {
                    var existing = await _context.ProductVariants.FindAsync(item.Id);
                    if (existing == null) _context.ProductVariants.Add(item);
                    else _context.Entry(existing).CurrentValues.SetValues(item);
                }
                _logger.LogInformation("Synced {Count} product variants", variants.Count);
            }

            // Product Images
            var imgRes = await client.GetAsync("/api/internal/product-images/all");
            if (imgRes.IsSuccessStatusCode)
            {
                var images = JsonSerializer.Deserialize<List<ProductImage>>(await imgRes.Content.ReadAsStringAsync(), opts) ?? new();
                foreach (var item in images)
                {
                    var existing = await _context.ProductImages.FindAsync(item.Id);
                    if (existing == null) _context.ProductImages.Add(item);
                    else _context.Entry(existing).CurrentValues.SetValues(item);
                }
                _logger.LogInformation("Synced {Count} product images", images.Count);
            }

            // Product Tags
            var ptRes = await client.GetAsync("/api/internal/product-tags/all");
            if (ptRes.IsSuccessStatusCode)
            {
                var productTags = JsonSerializer.Deserialize<List<ProductTag>>(await ptRes.Content.ReadAsStringAsync(), opts) ?? new();
                foreach (var item in productTags)
                {
                    var existing = await _context.ProductTags.FindAsync(item.ProductId, item.TagId);
                    if (existing == null) _context.ProductTags.Add(item);
                    else _context.Entry(existing).CurrentValues.SetValues(item);
                }
                _logger.LogInformation("Synced {Count} product tags", productTags.Count);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Full resync products completed: {Total} products synced", totalSynced);
            return Ok(new { success = true, message = $"Đã resync {totalSynced} products + shops + variants + images + tags từ Main API." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during full resync products");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
