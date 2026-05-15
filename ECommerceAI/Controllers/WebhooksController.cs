using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECommerceAI.Data;
using ECommerceAI.Data.Entities.ReadOnly;

namespace ECommerceAI.Controllers;

[ApiController]
[Route("api/ai/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly AiDbContext _context;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(AiDbContext context, ILogger<WebhooksController> logger)
    {
        _context = context;
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
            switch (payload.Table.ToLower())
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
}
