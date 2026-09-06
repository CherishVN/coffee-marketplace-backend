namespace ECommerceAPI.Application.DTOs.AI;

/// <summary>
/// Request gửi đến AI service để gợi ý category
/// </summary>
public class AiCategorySuggestionRequest
{
    /// <summary>
    /// Tiêu đề sản phẩm
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Mô tả sản phẩm
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Danh sách URLs hình ảnh sản phẩm (optional)
    /// </summary>
    public List<string>? ImageUrls { get; set; }

    /// <summary>
    /// Giá sản phẩm (có thể giúp AI đoán category)
    /// </summary>
    public decimal? Price { get; set; }
}
