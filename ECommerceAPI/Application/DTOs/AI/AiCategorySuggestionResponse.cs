namespace ECommerceAPI.Application.DTOs.AI;

/// <summary>
/// Response từ AI service với gợi ý category
/// </summary>
public class AiCategorySuggestionResponse
{
    /// <summary>
    /// ID category được gợi ý
    /// </summary>
    public long? SuggestedCategoryId { get; set; }

    /// <summary>
    /// Tên category được gợi ý
    /// </summary>
    public string? SuggestedCategoryName { get; set; }

    /// <summary>
    /// Độ tin cậy của gợi ý (0-1)
    /// </summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// Các category thay thế khác (sorted by confidence)
    /// </summary>
    public List<CategoryOption>? AlternativeCategories { get; set; }

    /// <summary>
    /// Tags được gợi ý
    /// </summary>
    public List<string>? SuggestedTags { get; set; }

    /// <summary>
    /// Keywords giúp tăng độ khám phá sản phẩm
    /// </summary>
    public List<string>? SuggestedKeywords { get; set; }

    /// <summary>
    /// Thời gian xử lý (ms)
    /// </summary>
    public int ProcessingTimeMs { get; set; }
}

public class CategoryOption
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
}
