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

    [HttpGet("debug/list-models")]
    public async Task<IActionResult> ListModels()
    {
        var result = await _gemini.ListModelsAsync();
        return Ok(new { models = result.Split('\n') });
    }

    [HttpPost("generate-report")]
    public async Task<IActionResult> GenerateReport([FromBody] GenerateReportRequestDto dto)
    {
        var validTypes = new[] { "sales", "sellers", "products", "customers", "disputes" };
        if (!validTypes.Contains(dto.ReportType.ToLower()))
            return BadRequest(new { message = $"Report type khong hop le. Chon: {string.Join(", ", validTypes)}" });

        var result = await _adminService.GenerateReportAsync(dto);
        return Ok(result);
    }

    [HttpPost("analyze-trends")]
    public async Task<IActionResult> AnalyzeTrends([FromBody] AnalyzeTrendsRequestDto dto)
    {
        var result = await _adminService.AnalyzeTrendsAsync(dto);
        return Ok(result);
    }

    [HttpPost("detect-anomalies")]
    public async Task<IActionResult> DetectAnomalies([FromBody] DetectAnomaliesRequestDto dto)
    {
        var allowedDataTypes = new[] { "orders", "revenue", "users", "products" };
        var dt = (dto.DataType ?? "orders").Trim().ToLowerInvariant();
        if (!allowedDataTypes.Contains(dt))
            return BadRequest(new { message = $"dataType phai la mot trong: {string.Join(", ", allowedDataTypes)}" });
        dto.DataType = dt;

        var result = await _adminService.DetectAnomaliesAsync(dto);
        return Ok(result);
    }

    [HttpPost("predict-metrics")]
    public async Task<IActionResult> PredictMetrics([FromBody] PredictMetricsRequestDto dto)
    {
        if (dto.ForecastDays < 1 || dto.ForecastDays > 90)
            return BadRequest(new { message = "ForecastDays phai tu 1 den 90" });

        var allowedMetrics = new[] { "revenue", "orders", "users", "products" };
        var m = (dto.Metric ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(m) || !allowedMetrics.Contains(m))
            return BadRequest(new { message = $"metric phai la mot trong: {string.Join(", ", allowedMetrics)}" });
        dto.Metric = m;

        var result = await _adminService.PredictMetricsAsync(dto);
        return Ok(result);
    }

    [HttpPost("summarize-disputes")]
    public async Task<IActionResult> SummarizeDisputes([FromBody] SummarizeDisputesRequestDto dto)
    {
        var result = await _adminService.SummarizeDisputesAsync(dto);
        return Ok(result);
    }

    [HttpGet("insights/dashboard")]
    public async Task<IActionResult> GetDashboardInsights()
    {
        var result = await _adminService.GetDashboardInsightsAsync();
        return Ok(result);
    }
}
