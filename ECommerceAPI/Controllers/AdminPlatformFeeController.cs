using System.Security.Claims;
using ECommerceAPI.Application.DTOs.Admin;
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
    private readonly IPlatformFeeConfigService _platformFeeConfigService;

    public AdminPlatformFeeController(
        IPlatformFeeReportService platformFeeReportService,
        IPlatformFeeConfigService platformFeeConfigService)
    {
        _platformFeeReportService = platformFeeReportService;
        _platformFeeConfigService = platformFeeConfigService;
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

    /// <summary>
    /// Lấy cấu hình phí sàn đang áp dụng.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var current = await _platformFeeConfigService.GetCurrentAsync();
        return Ok(new { success = true, data = current });
    }

    /// <summary>
    /// Cập nhật tỷ lệ phí sàn. Lưu lịch sử thay đổi.
    /// </summary>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdatePlatformFeeConfigRequest request)
    {
        if (request.CommissionPercent < 0 || request.CommissionPercent > 100)
            return BadRequest(new { success = false, message = "CommissionPercent phải trong khoảng 0–100." });

        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _platformFeeConfigService.UpdateAsync(request, adminId);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Lịch sử thay đổi tỷ lệ phí sàn (phân trang).
    /// </summary>
    [HttpGet("settings/history")]
    public async Task<IActionResult> GetSettingsHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _platformFeeConfigService.GetHistoryAsync(page, pageSize);
        return Ok(new
        {
            success = true,
            data = new
            {
                items,
                totalCount = total,
                page,
                pageSize
            }
        });
    }
}
