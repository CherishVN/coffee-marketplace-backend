using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Controllers;

/// <summary>
/// Public tag endpoints — no authentication required.
/// Used by the customer-facing storefront for filtering.
/// </summary>
[ApiController]
[Route("api/tags")]
public class TagsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TagsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>Lấy danh sách tất cả tags đang active (dùng cho bộ lọc sản phẩm)</summary>
    [HttpGet]
    public async Task<IActionResult> GetTags([FromQuery] string? search = null)
    {
        var query = _context.Tags.AsQueryable().Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.Contains(search));

        var tags = await query
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                id = t.Id,
                name = t.Name,
                slug = t.Slug,
                isActive = t.IsActive,
                productCount = t.ProductTags.Count
            })
            .ToListAsync();

        return Ok(new { success = true, data = tags });
    }
}
