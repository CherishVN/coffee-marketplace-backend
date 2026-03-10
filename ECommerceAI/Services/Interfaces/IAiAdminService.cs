using ECommerceAI.DTOs.Admin;

namespace ECommerceAI.Services.Interfaces;

public interface IAiAdminService
{
    Task<GenerateReportResponseDto> GenerateReportAsync(GenerateReportRequestDto request);
    Task<AnalyzeTrendsResponseDto> AnalyzeTrendsAsync(AnalyzeTrendsRequestDto request);
    Task<DetectAnomaliesResponseDto> DetectAnomaliesAsync(DetectAnomaliesRequestDto request);
    Task<PredictMetricsResponseDto> PredictMetricsAsync(PredictMetricsRequestDto request);
    Task<SummarizeDisputesResponseDto> SummarizeDisputesAsync(SummarizeDisputesRequestDto request);
    Task<DashboardInsightsResponseDto> GetDashboardInsightsAsync();
}
