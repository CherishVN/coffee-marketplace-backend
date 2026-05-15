using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Controllers;

/// <summary>
/// Endpoint nội bộ dành cho AI Service gọi để resync dữ liệu khi cần.
/// Được bảo vệ bằng X-Internal-Key header.
/// </summary>
[ApiController]
[Route("api/internal")]
public class InternalSyncController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public InternalSyncController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    private bool IsAuthorized()
    {
        var expectedKey = _configuration["InternalAuth:ApiKey"];
        Request.Headers.TryGetValue("X-Internal-Key", out var providedKey);
        return !string.IsNullOrEmpty(expectedKey) && expectedKey == providedKey.ToString();
    }

    /// <summary>
    /// Trả về role code của một user. AI Service gọi endpoint này thay vì query DB trực tiếp.
    /// Kết quả được cache ở phía AI để tránh gọi lặp lại.
    /// </summary>
    [HttpGet("users/{userId}/role")]
    public async Task<IActionResult> GetUserRole(Guid userId)
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "Invalid internal key" });

        var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.RoleId })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(new { message = "User not found" });

        string roleCode = user.RoleId switch
        {
            1 => "admin",
            2 => "seller",
            _ => "customer"
        };

        return Ok(new { userId, roleCode });
    }

    /// <summary>
    /// Trả về toàn bộ danh sách users để AI Service resync.
    /// Chỉ cho phép gọi nội bộ với X-Internal-Key.
    /// </summary>
    [HttpGet("users/all")]
    public async Task<IActionResult> GetAllUsers()
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "Invalid internal key" });

        var users = await _context.Users
            .Select(u => new
            {
                id = u.Id,
                user_code = u.UserCode,
                full_name = u.FullName,
                phone = u.Phone,
                role_id = u.RoleId,
                status = u.Status,
                created_at = u.CreatedAt,
                updated_at = u.UpdatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Trả về toàn bộ danh sách shops để AI Service resync.
    /// </summary>
    [HttpGet("shops/all")]
    public async Task<IActionResult> GetAllShops()
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "Invalid internal key" });

        var shops = await _context.Shops
            .Select(s => new
            {
                id = s.Id,
                owner_id = s.OwnerId,
                name = s.Name,
                status = s.Status,
                verification_status = s.VerificationStatus,
                created_at = s.CreatedAt
            })
            .ToListAsync();

        return Ok(shops);
    }

    /// <summary>
    /// Trả về toàn bộ danh sách products để AI Service resync.
    /// </summary>
    [HttpGet("products/all")]
    public async Task<IActionResult> GetAllProducts()
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "Invalid internal key" });

        var products = await _context.Products
            .Select(p => new
            {
                id = p.Id,
                shop_id = p.ShopId,
                category_id = p.CategoryId,
                name = p.Name,
                description = p.Description,
                base_price = p.BasePrice,
                currency = p.Currency,
                status = p.Status,
                created_at = p.CreatedAt,
                updated_at = p.UpdatedAt
            })
            .ToListAsync();

        return Ok(products);
    }
}
