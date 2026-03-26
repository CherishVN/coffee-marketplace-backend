using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Controllers;

/// <summary>
/// Public material endpoints — no authentication required.
/// Used by the customer-facing storefront for filtering.
/// </summary>
[ApiController]
[Route("api/materials")]
public class MaterialsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MaterialsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>Lấy danh sách tất cả chất liệu đang active (dùng cho bộ lọc sản phẩm)</summary>
    [HttpGet]
    public async Task<IActionResult> GetMaterials([FromQuery] string? search = null)
    {
        var query = _context.Materials
            .Where(m => m.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Name.Contains(search));

        var materials = await query
            .OrderBy(m => m.Name)
            .Select(m => new
            {
                id = m.Id,
                name = m.Name,
                slug = m.Slug,
                description = m.Description,
                productCount = m.ProductMaterials.Count
            })
            .ToListAsync();

        return Ok(new { success = true, data = materials });
    }
}
