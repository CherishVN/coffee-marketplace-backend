namespace ECommerceAPI.Infrastructure.Configuration;

/// <summary>
/// Cấu hình FPT.AI — đọc CCCD/CMND (Vietnam ID Reader).
/// </summary>
public class FptAiSettings
{
    public const string SectionName = "FptAi";

    /// <summary>API key từ console.fpt.ai (không đưa lên front-end)</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Endpoint mặc định theo tài liệu FPT</summary>
    public string IdReaderUrl { get; set; } = "https://api.fpt.ai/vision/idr/vnm/";

    public int TimeoutSeconds { get; set; } = 60;
}
