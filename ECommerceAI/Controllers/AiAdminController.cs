using ECommerceAI.DTOs.Admin;
using ECommerceAI.Services;
using ECommerceAI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAI.Controllers;

[ApiController]
[Route("api/ai/admin")]
[Authorize(Roles = "admin")]
public class AiAdminController : ControllerBase
{
    private readonly IAiAdminService _adminService;
    private readonly GeminiClientService _gemini;

    public AiAdminController(IAiAdminService adminService, GeminiClientService gemini)
    {
        _adminService = adminService;
        _gemini = gemini;
    }

    /// <summary>[DEBUG] Liệt kê models Gemini khả dụng với API key hiện tại</summary>
    [HttpGet("debug/list-models")]
    public async Task<IActionResult> ListModels()
    {
        var result = await _gemini.ListModelsAsync();
        return Ok(new { models = result.Split('\n') });
    }

    /// <summary>Tự động tạo báo cáo theo loại và khoảng thời gian</summary>
    [HttpPost("generate-report")]
    public async Task<IActionResult> GenerateReport([FromBody] GenerateReportRequestDto dto)
    {
        var validTypes = new[] { "sales", "sellers", "products", "customers", "disputes" };
        if (!validTypes.Contains(dto.ReportType.ToLower()))
            return BadRequest(new { message = $"Report type không hợp lệ. Chọn: {string.Join(", ", validTypes)}" });

        var result = await _adminService.GenerateReportAsync(dto);
        return Ok(result);
    }

    /// <summary>Phân tích xu hướng đa chiều</summary>
    [HttpPost("analyze-trends")]
    public async Task<IActionResult> AnalyzeTrends([FromBody] AnalyzeTrendsRequestDto dto)
    {
        var result = await _adminService.AnalyzeTrendsAsync(dto);
        return Ok(result);
    }

    /// <summary>Phát hiện bất thường trong dữ liệu</summary>
    [HttpPost("detect-anomalies")]
    public async Task<IActionResult> DetectAnomalies([FromBody] DetectAnomaliesRequestDto dto)
    {
        var result = await _adminService.DetectAnomaliesAsync(dto);
        return Ok(result);
    }

    /// <summary>Dự đoán chỉ số kinh doanh</summary>
    [HttpPost("predict-metrics")]
    public async Task<IActionResult> PredictMetrics([FromBody] PredictMetricsRequestDto dto)
    {
        if (dto.ForecastDays < 1 || dto.ForecastDays > 90)
            return BadRequest(new { message = "ForecastDays phải từ 1 đến 90" });

        var result = await _adminService.PredictMetricsAsync(dto);
        return Ok(result);
    }

    /// <summary>Tóm tắt và phân tích khiếu nại</summary>
    [HttpPost("summarize-disputes")]
    public async Task<IActionResult> SummarizeDisputes([FromBody] SummarizeDisputesRequestDto dto)
    {
        var result = await _adminService.SummarizeDisputesAsync(dto);
        return Ok(result);
    }

    /// <summary>Lấy AI insights cho dashboard</summary>
    [HttpGet("insights/dashboard")]
    public async Task<IActionResult> GetDashboardInsights()
    {
        var result = await _adminService.GetDashboardInsightsAsync();
        return Ok(result);
    }
}
