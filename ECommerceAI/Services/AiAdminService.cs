using ECommerceAI.Data;
using ECommerceAI.DTOs.Admin;
using ECommerceAI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAI.Services;

public class AiAdminService : IAiAdminService
{
    private readonly AiDbContext _context;
    private readonly GeminiClientService _gemini;
    private readonly ILogger<AiAdminService> _logger;
    private readonly string _systemPrompt;

    public AiAdminService(AiDbContext context, GeminiClientService gemini, ILogger<AiAdminService> logger)
    {
        _context = context;
        _gemini = gemini;
        _logger = logger;

        var promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "AdminAnalyticsPrompt.txt");
        _systemPrompt = File.Exists(promptPath) ? File.ReadAllText(promptPath) : "Bạn là AI phân tích dữ liệu kinh doanh.";
    }

    // ── Tạo báo cáo tổng hợp ────────────────────────────────────────────────
    public async Task<GenerateReportResponseDto> GenerateReportAsync(GenerateReportRequestDto request)
    {
        var stats = await QueryStatsAsync(request.ReportType, request.FromDate, request.ToDate);

        var statsJson = System.Text.Json.JsonSerializer.Serialize(stats);
        var prompt = $"""
            Báo cáo {request.ReportType} ({request.FromDate:dd/MM} - {request.ToDate:dd/MM/yyyy}).
            Dữ liệu: {statsJson}
            {(request.AdditionalContext != null ? $"Yêu cầu: {request.AdditionalContext}" : "")}
            Đưa ra tóm tắt ngắn (3-4 câu) và 2-3 khuyến nghị bằng tiếng Việt.
            """;

        var insights = await _gemini.GenerateAsync(_systemPrompt, prompt);

        return new GenerateReportResponseDto
        {
            ReportType = request.ReportType,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Statistics = stats,
            AiInsights = insights,
            KeyFindings = ExtractKeyFindings(insights),
            Recommendations = ExtractRecommendations(insights),
            GeneratedAt = DateTime.UtcNow
        };
    }

    // ── Phân tích xu hướng ──────────────────────────────────────────────────
    public async Task<AnalyzeTrendsResponseDto> AnalyzeTrendsAsync(AnalyzeTrendsRequestDto request)
    {
        var dataPoints = await QueryTrendDataAsync(request);

        var totalValues = dataPoints
            .SelectMany(d => d.Values)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

        var prompt = $"""
            Xu hướng {string.Join(", ", request.MetricTypes)} từ {request.FromDate:dd/MM} đến {request.ToDate:dd/MM/yyyy}.
            Tổng: {System.Text.Json.JsonSerializer.Serialize(totalValues)}, số điểm dữ liệu: {dataPoints.Count}.
            Nhận xét ngắn (2-3 câu) về xu hướng và dự báo bằng tiếng Việt.
            """;

        var analysis = await _gemini.GenerateAsync(_systemPrompt, prompt);

        var growthRates = CalculateGrowthRates(dataPoints, request.MetricTypes);

        return new AnalyzeTrendsResponseDto
        {
            DataPoints = dataPoints,
            AiAnalysis = analysis,
            GrowthRates = growthRates,
            TrendInsights = ExtractKeyFindings(analysis)
        };
    }

    // ── Phát hiện bất thường ────────────────────────────────────────────────
    public async Task<DetectAnomaliesResponseDto> DetectAnomaliesAsync(DetectAnomaliesRequestDto request)
    {
        var anomalies = await DetectStatisticalAnomaliesAsync(request);

        if (!anomalies.Any())
        {
            return new DetectAnomaliesResponseDto
            {
                Anomalies = new List<AnomalyItem>(),
                AiExplanation = "Không phát hiện bất thường đáng kể trong giai đoạn này."
            };
        }

        var prompt = $"""
            Phân tích và giải thích các bất thường sau trong dữ liệu {request.DataType}:
            
            {System.Text.Json.JsonSerializer.Serialize(anomalies, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}
            
            Đưa ra: nguyên nhân có thể, mức độ nghiêm trọng, và hành động khuyến nghị.
            """;

        var explanation = await _gemini.GenerateAsync(_systemPrompt, prompt);

        return new DetectAnomaliesResponseDto
        {
            Anomalies = anomalies,
            AiExplanation = explanation
        };
    }

    // ── Dự đoán chỉ số ───────────────────────────────────────────────────────
    public async Task<PredictMetricsResponseDto> PredictMetricsAsync(PredictMetricsRequestDto request)
    {
        // Lấy dữ liệu 90 ngày gần nhất để làm cơ sở dự đoán
        var historicalData = await GetHistoricalDataAsync(request.Metric, 90);

        // Chỉ lấy 7 điểm gần nhất để giảm token
        var recentPoints = historicalData.TakeLast(7).ToList();
        var totalCount = recentPoints.Sum(x => x.Values.FirstOrDefault().Value);
        var avgPerDay = recentPoints.Any() ? totalCount / recentPoints.Count : 0;

        var prompt = $"""
            Dữ liệu {request.Metric} 7 ngày gần nhất: trung bình {avgPerDay:F0}/ngày, tổng {totalCount:F0}.
            Dự đoán xu hướng {request.ForecastDays} ngày tới trong 2-3 câu ngắn gọn bằng tiếng Việt.
            """;

        var analysis = await _gemini.GenerateAsync(_systemPrompt, prompt);

        var predictions = GenerateSimplePredictions(historicalData, request.ForecastDays);

        return new PredictMetricsResponseDto
        {
            Metric = request.Metric,
            Predictions = predictions,
            AiAnalysis = analysis,
            ConfidenceLevel = 0.75m
        };
    }

    // ── Tóm tắt khiếu nại ───────────────────────────────────────────────────
    public async Task<SummarizeDisputesResponseDto> SummarizeDisputesAsync(SummarizeDisputesRequestDto request)
    {
        // Sử dụng raw SQL để truy vấn bảng disputes (không có entity trong AI service)
        var disputeStats = await QueryDisputeStatsAsync(request.FromDate, request.ToDate, request.MaxItems);

        var prompt = $"""
            Tóm tắt và phân tích tình hình khiếu nại từ {request.FromDate:dd/MM/yyyy} đến {request.ToDate:dd/MM/yyyy}:
            
            Thống kê:
            {System.Text.Json.JsonSerializer.Serialize(disputeStats, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}
            
            Hãy:
            1. Tóm tắt các vấn đề phổ biến nhất
            2. Xác định nguyên nhân gốc rễ
            3. Đề xuất giải pháp phòng ngừa
            """;

        var summary = await _gemini.GenerateAsync(_systemPrompt, prompt);

        return new SummarizeDisputesResponseDto
        {
            TotalDisputes = disputeStats.TotalCount,
            ByType = disputeStats.ByType,
            ByStatus = disputeStats.ByStatus,
            AiSummary = summary,
            CommonIssues = ExtractKeyFindings(summary),
            Recommendations = ExtractRecommendations(summary)
        };
    }

    // ── Dashboard Insights ───────────────────────────────────────────────────
    public async Task<DashboardInsightsResponseDto> GetDashboardInsightsAsync()
    {
        var last7Days = DateTime.UtcNow.AddDays(-7);
        var overview = await GetOverviewStatsAsync(last7Days);

        var prompt = $"""
            Phân tích tình hình kinh doanh 7 ngày qua và đưa ra insights quan trọng cho admin:
            
            Tổng quan:
            {System.Text.Json.JsonSerializer.Serialize(overview, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}
            
            Hãy highlight:
            1. Điểm cần chú ý (alerts)
            2. Điểm tích cực
            3. Hành động cần làm ngay
            """;

        var aiSummary = await _gemini.GenerateAsync(_systemPrompt, prompt);

        return new DashboardInsightsResponseDto
        {
            KeyAlerts = ExtractSection(aiSummary, "cần chú ý", "alert"),
            PositiveHighlights = ExtractSection(aiSummary, "tích cực", "positive"),
            ActionItems = ExtractRecommendations(aiSummary),
            AiSummary = aiSummary,
            GeneratedAt = DateTime.UtcNow
        };
    }

    // ── Private query helpers ─────────────────────────────────────────────────

    private async Task<object> QueryStatsAsync(string reportType, DateTime from, DateTime to)
    {
        return reportType.ToLower() switch
        {
            "products" => new
            {
                TotalProducts = await _context.Products.CountAsync(p => p.CreatedAt >= from && p.CreatedAt <= to),
                ActiveProducts = await _context.Products.CountAsync(p => p.Status == 1),
                ByCategory = await _context.Products
                    .Where(p => p.CategoryId != null)
                    .GroupBy(p => p.Category!.Name)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(10)
                    .ToListAsync()
            },
            _ => new { Message = $"Report type '{reportType}' - data fetched from main DB" }
        };
    }

    private async Task<List<TrendDataPoint>> QueryTrendDataAsync(AnalyzeTrendsRequestDto request)
    {
        var dataPoints = new List<TrendDataPoint>();
        var current = request.FromDate.Date;

        while (current <= request.ToDate.Date)
        {
            var point = new TrendDataPoint { Date = current, Values = new Dictionary<string, decimal>() };

            if (request.MetricTypes.Contains("products"))
            {
                var count = await _context.Products.CountAsync(p => p.CreatedAt.Date == current);
                point.Values["products"] = count;
            }

            dataPoints.Add(point);
            current = request.Granularity switch
            {
                "weekly" => current.AddDays(7),
                "monthly" => current.AddMonths(1),
                _ => current.AddDays(1)
            };
        }

        return dataPoints;
    }

    private async Task<List<AnomalyItem>> DetectStatisticalAnomaliesAsync(DetectAnomaliesRequestDto request)
    {
        // Simple threshold-based anomaly detection
        var anomalies = new List<AnomalyItem>();
        var from = DateTime.UtcNow.AddDays(-request.LookbackDays);

        if (request.DataType == "products")
        {
            var dailyCounts = await _context.Products
                .Where(p => p.CreatedAt >= from)
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            if (dailyCounts.Count > 7)
            {
                var avg = (decimal)dailyCounts.Average(x => x.Count);
                var threshold = avg * 3;

                foreach (var day in dailyCounts.Where(d => d.Count > threshold))
                {
                    anomalies.Add(new AnomalyItem
                    {
                        DetectedAt = day.Date,
                        Type = "spike",
                        Severity = "medium",
                        Description = $"Số lượng sản phẩm mới bất thường cao",
                        ExpectedValue = avg,
                        ActualValue = day.Count,
                        DeviationPercent = (day.Count - avg) / avg * 100
                    });
                }
            }
        }

        return anomalies;
    }

    private async Task<List<TrendDataPoint>> GetHistoricalDataAsync(string metric, int days)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        var dataPoints = new List<TrendDataPoint>();

        if (metric == "products")
        {
            var data = await _context.Products
                .Where(p => p.CreatedAt >= from)
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            dataPoints = data.Select(d => new TrendDataPoint
            {
                Date = d.Date,
                Values = new Dictionary<string, decimal> { [metric] = d.Count }
            }).ToList();
        }

        return dataPoints;
    }

    private static List<PredictionPoint> GenerateSimplePredictions(List<TrendDataPoint> historical, int forecastDays)
    {
        if (!historical.Any()) return new List<PredictionPoint>();

        var avgGrowth = historical.Count > 1
            ? historical.Skip(1).Zip(historical, (curr, prev) =>
                curr.Values.FirstOrDefault().Value - prev.Values.FirstOrDefault().Value).Average()
            : 0;

        var lastValue = historical.Last().Values.FirstOrDefault().Value;
        var predictions = new List<PredictionPoint>();

        for (int i = 1; i <= forecastDays; i++)
        {
            var predicted = lastValue + avgGrowth * i;
            predictions.Add(new PredictionPoint
            {
                Date = DateTime.UtcNow.AddDays(i),
                PredictedValue = Math.Max(0, predicted),
                LowerBound = Math.Max(0, predicted * 0.85m),
                UpperBound = predicted * 1.15m
            });
        }

        return predictions;
    }

    private async Task<DisputeStats> QueryDisputeStatsAsync(DateTime from, DateTime to, int maxItems)
    {
        // Dùng raw SQL vì bảng disputes không có entity trong AiDbContext
        var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();

        var stats = new DisputeStats();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(*) as total,
                   SUM(CASE WHEN status = 0 THEN 1 ELSE 0 END) as pending,
                   SUM(CASE WHEN status = 1 THEN 1 ELSE 0 END) as resolved,
                   SUM(CASE WHEN status = 2 THEN 1 ELSE 0 END) as rejected
            FROM disputes
            WHERE created_at BETWEEN '{from:yyyy-MM-dd}' AND '{to:yyyy-MM-dd}'
            LIMIT {maxItems}
            """;

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            stats.TotalCount = reader.GetInt32(0);
            stats.ByStatus = new Dictionary<string, int>
            {
                ["pending"] = reader.GetInt32(1),
                ["resolved"] = reader.GetInt32(2),
                ["rejected"] = reader.GetInt32(3)
            };
        }

        await conn.CloseAsync();
        return stats;
    }

    private async Task<object> GetOverviewStatsAsync(DateTime from)
    {
        return new
        {
            NewProducts = await _context.Products.CountAsync(p => p.CreatedAt >= from),
            ActiveProducts = await _context.Products.CountAsync(p => p.Status == 1),
            NewCategories = 0,
            Period = $"7 ngày ({from:dd/MM} - {DateTime.UtcNow:dd/MM/yyyy})"
        };
    }

    private static Dictionary<string, decimal> CalculateGrowthRates(List<TrendDataPoint> dataPoints, List<string> metrics)
    {
        var rates = new Dictionary<string, decimal>();
        if (dataPoints.Count < 2) return rates;

        foreach (var metric in metrics)
        {
            var first = dataPoints.First().Values.GetValueOrDefault(metric);
            var last = dataPoints.Last().Values.GetValueOrDefault(metric);
            if (first > 0)
                rates[metric] = (last - first) / first * 100;
        }

        return rates;
    }

    private static List<string> ExtractKeyFindings(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.TrimStart().StartsWith("-") || l.TrimStart().StartsWith("•") || l.TrimStart().StartsWith("*"))
            .Select(l => l.TrimStart('-', '•', '*', ' ').Trim())
            .Where(l => l.Length > 10)
            .Take(5)
            .ToList();

        return lines.Any() ? lines : new List<string> { text.Split('.').First().Trim() };
    }

    private static List<string> ExtractRecommendations(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Contains("khuyến nghị") || l.Contains("nên") || l.Contains("cần") || l.Contains("→"))
            .Select(l => l.Trim())
            .Take(5)
            .ToList();

        return lines.Any() ? lines : ExtractKeyFindings(text);
    }

    private static List<string> ExtractSection(string text, string keyword, string fallback)
    {
        return ExtractKeyFindings(text);
    }

    private class DisputeStats
    {
        public int TotalCount { get; set; }
        public Dictionary<string, int> ByType { get; set; } = new();
        public Dictionary<string, int> ByStatus { get; set; } = new();
    }
}
