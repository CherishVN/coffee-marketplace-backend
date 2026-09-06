namespace ECommerceAPI.Application.DTOs.AI;

/// <summary>
/// Response gợi ý tags từ AI
/// </summary>
public class AiTagSuggestionResponse
{
    /// <summary>
    /// Danh sách tags được gợi ý
    /// </summary>
    public List<TagSuggestion> SuggestedTags { get; set; } = new();

    /// <summary>
    /// Thời gian xử lý
    /// </summary>
    public int ProcessingTimeMs { get; set; }
}

public class TagSuggestion
{
    /// <summary>
    /// Tên tag
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    /// Độ tin cậy
    /// </summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// Loại tag (style, material, attribute, etc.)
    /// </summary>
    public string? TagType { get; set; }
}
