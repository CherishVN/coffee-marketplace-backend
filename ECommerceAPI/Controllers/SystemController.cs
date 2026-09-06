using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public SystemController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    /// <summary>Health check — kiểm tra trạng thái server và database</summary>
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var dbStatus = "ok";
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT 1");
        }
        catch
        {
            dbStatus = "error";
        }

        var uptime = DateTime.UtcNow - _startTime;
        var status = dbStatus == "ok" ? "healthy" : "degraded";

        var result = new
        {
            status,
            timestamp = DateTime.UtcNow,
            uptime = $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s",
            services = new
            {
                database = dbStatus,
                api = "ok"
            },
            version = "1.0.0"
        };

        return dbStatus == "ok" ? Ok(result) : StatusCode(503, result);
    }

    /// <summary>Lấy cấu hình public của hệ thống (không bao gồm secrets)</summary>
    [HttpGet("config")]
    public IActionResult Config()
    {
        return Ok(new
        {
            success = true,
            data = new
            {
                maxImageUploadMb = 5,
                maxImagesPerProduct = 10,
                maxCartItems = 50,
                currency = "VND",
                disputeWindowDays = 7,
                supportEmail = _configuration["Support:Email"] ?? "support@ecap.vn",
                maintenanceMode = false
            }
        });
    }
}
