namespace ECommerceAPI.Infrastructure.Configuration;

/// <summary>
/// Cấu hình kết nối với AI Microservice
/// </summary>
public class AiServiceSettings
{
    public const string SectionName = "AiService";

    /// <summary>
    /// Base URL của AI service
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Các endpoints API
    /// </summary>
    public AiEndpoints Endpoints { get; set; } = new();

    /// <summary>
    /// Timeout cho HTTP requests (giây)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// API Key dùng cho Admin và Customer
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API Key riêng dùng cho Seller
    /// </summary>
    public string SellerApiKey { get; set; } = string.Empty;
}

public class AiEndpoints
{
    /// <summary>
    /// Endpoint gợi ý category
    /// </summary>
    public string CategorySuggestion { get; set; } = "/api/ai/suggest-category";

    /// <summary>
    /// Endpoint gợi ý tags
    /// </summary>
    public string TagSuggestion { get; set; } = "/api/ai/suggest-tags";

    /// <summary>
    /// Endpoint gợi ý materials
    /// </summary>
    public string MaterialSuggestion { get; set; } = "/api/ai/suggest-materials";
}
