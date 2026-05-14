using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Controllers;

/// <summary>
/// Public endpoint — trả về danh sách hồ sơ đặc sản địa phương.
/// FE dùng để populate form chọn "vùng + loại" khi seller tạo sản phẩm.
/// </summary>
[ApiController]
[Route("api/local-specialty-profiles")]
public class LocalSpecialtyController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LocalSpecialtyController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy tất cả hồ sơ đặc sản, có thể lọc theo categoryCode (vd: "ca_phe").
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProfiles([FromQuery] string? categoryCode = null)
    {
        var query = _context.LocalSpecialtyProfiles
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(categoryCode))
            query = query.Where(p => p.CategoryCode == categoryCode.ToLower().Trim());

        var profiles = await query
            .OrderBy(p => p.ProvinceName)
            .Select(p => new
            {
                p.Id,
                p.CategoryCode,
                p.ProvinceName,
                p.ArchetypeName,
                p.DisplayNote,
                ExpectedTraits = p.ExpectedTraitsPipe.Split('|', StringSplitOptions.RemoveEmptyEntries),
                Keywords = p.KeywordsPipe.Split('|', StringSplitOptions.RemoveEmptyEntries),
            })
            .ToListAsync();

        return Ok(new { success = true, data = profiles });
    }

    /// <summary>
    /// Lấy hồ sơ theo ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProfileById(int id)
    {
        var profile = await _context.LocalSpecialtyProfiles
            .Where(p => p.Id == id && p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.CategoryCode,
                p.ProvinceName,
                p.ArchetypeName,
                p.DisplayNote,
                ExpectedTraits = p.ExpectedTraitsPipe.Split('|', StringSplitOptions.RemoveEmptyEntries),
                Keywords = p.KeywordsPipe.Split('|', StringSplitOptions.RemoveEmptyEntries),
            })
            .FirstOrDefaultAsync();

        if (profile == null)
            return NotFound(new { success = false, message = "Không tìm thấy hồ sơ đặc sản" });

        return Ok(new { success = true, data = profile });
    }
}
