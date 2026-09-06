using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public AdminDashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Get dashboard statistics overview
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await _dashboardService.GetDashboardStatsAsync();
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Get recent activities
    /// </summary>
    [HttpGet("recent-activities")]
    public async Task<IActionResult> GetRecentActivities([FromQuery] int limit = 10)
    {
        var result = await _dashboardService.GetRecentActivitiesAsync(limit);
        return Ok(new { success = true, activities = result });
    }

    /// <summary>
    /// Get top performing shops
    /// </summary>
    [HttpGet("top-shops")]
    public async Task<IActionResult> GetTopShops([FromQuery] int limit = 10)
    {
        var result = await _dashboardService.GetTopShopsAsync(limit);
        return Ok(new { success = true, shops = result });
    }

    /// <summary>
    /// Get top selling products
    /// </summary>
    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProducts([FromQuery] int limit = 10)
    {
        var result = await _dashboardService.GetTopProductsAsync(limit);
        return Ok(new { success = true, products = result });
    }
}
