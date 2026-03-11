namespace ECommerceAI.DTOs.Admin;

// ── Generate Report ───────────────────────────────────────────────────────────

public class GenerateReportRequestDto
{
    public string ReportType { get; set; } = null!;  // sales | sellers | products | customers | disputes
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? AdditionalContext { get; set; }
}

public class GenerateReportResponseDto
{
    public string ReportType { get; set; } = null!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public object Statistics { get; set; } = null!;  // Raw stats từ DB
    public string AiInsights { get; set; } = null!;  // Text từ LLM
    public List<string> KeyFindings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

// ── Analyze Trends ────────────────────────────────────────────────────────────

public class AnalyzeTrendsRequestDto
{
    public List<string> MetricTypes { get; set; } = new();  // revenue | orders | sellers | products | customers
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string Granularity { get; set; } = "daily";  // daily | weekly | monthly
}

public class AnalyzeTrendsResponseDto
{
    public List<TrendDataPoint> DataPoints { get; set; } = new();
    public string AiAnalysis { get; set; } = null!;
    public Dictionary<string, decimal> GrowthRates { get; set; } = new();
    public List<string> TrendInsights { get; set; } = new();
}

public class TrendDataPoint
{
    public DateTime Date { get; set; }
    public Dictionary<string, decimal> Values { get; set; } = new();
}

// ── Detect Anomalies ──────────────────────────────────────────────────────────

public class DetectAnomaliesRequestDto
{
    public string DataType { get; set; } = "orders";  // orders | revenue | users
    public int LookbackDays { get; set; } = 30;
}

public class DetectAnomaliesResponseDto
{
    public List<AnomalyItem> Anomalies { get; set; } = new();
    public string AiExplanation { get; set; } = null!;
}

public class AnomalyItem
{
    public DateTime DetectedAt { get; set; }
    public string Type { get; set; } = null!;           // spike | drop | pattern_break
    public string Severity { get; set; } = "medium";    // low | medium | high | critical
    public string Description { get; set; } = null!;
    public decimal ExpectedValue { get; set; }
    public decimal ActualValue { get; set; }
    public decimal DeviationPercent { get; set; }
}

// ── Predict Metrics ───────────────────────────────────────────────────────────

public class PredictMetricsRequestDto
{
    public string Metric { get; set; } = null!;  // revenue | orders | users
    public int ForecastDays { get; set; } = 30;
}

public class PredictMetricsResponseDto
{
    public string Metric { get; set; } = null!;
    public List<PredictionPoint> Predictions { get; set; } = new();
    public string AiAnalysis { get; set; } = null!;
    public decimal ConfidenceLevel { get; set; }
}

public class PredictionPoint
{
    public DateTime Date { get; set; }
    public decimal PredictedValue { get; set; }
    public decimal LowerBound { get; set; }
    public decimal UpperBound { get; set; }
}

// ── Summarize Disputes ────────────────────────────────────────────────────────

public class SummarizeDisputesRequestDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int MaxItems { get; set; } = 100;
}

public class SummarizeDisputesResponseDto
{
    public int TotalDisputes { get; set; }
    public Dictionary<string, int> ByType { get; set; } = new();
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public string AiSummary { get; set; } = null!;
    public List<string> CommonIssues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

// ── Dashboard Insights ────────────────────────────────────────────────────────

public class DashboardInsightsResponseDto
{
    public List<string> KeyAlerts { get; set; } = new();
    public List<string> PositiveHighlights { get; set; } = new();
    public List<string> ActionItems { get; set; } = new();
    public string AiSummary { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
}
