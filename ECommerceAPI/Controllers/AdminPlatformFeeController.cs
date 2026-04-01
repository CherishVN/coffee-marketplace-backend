using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/platform-fees")]
[Authorize(Roles = "admin")]
public class AdminPlatformFeeController : ControllerBase
{
    private readonly IPlatformFeeReportService _platformFeeReportService;

    public AdminPlatformFeeController(IPlatformFeeReportService platformFeeReportService)
    {
        _platformFeeReportService = platformFeeReportService;
    }

    /// <summary>
    /// Tổng hợp phí sàn: tổng phí, tổng subtotal, tổng net seller. Lọc theo created_at (UTC).
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc)
    {
        var data = await _platformFeeReportService.GetSummaryAsync(fromUtc, toUtc);
        return Ok(new { success = true, data });
    }

    /// <summary>
    /// Danh sách bản ghi phí sàn (phân trang), kèm summary trong khoảng lọc (và shop nếu có shopId).
    /// </summary>
    [HttpGet("records")]
    public async Task<IActionResult> GetRecords(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] Guid? shopId = null)
    {
        var result = await _platformFeeReportService.GetRecordsAsync(page, pageSize, fromUtc, toUtc, shopId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(new { success = true, data = result });
    }
}
