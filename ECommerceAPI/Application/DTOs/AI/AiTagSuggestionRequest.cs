namespace ECommerceAPI.Application.DTOs.AI;

/// <summary>
/// Request gợi ý tags cho sản phẩm
/// </summary>
public class AiTagSuggestionRequest
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
    /// Category ID (nếu đã chọn)
    /// </summary>
    public long? CategoryId { get; set; }

    /// <summary>
    /// URLs hình ảnh
    /// </summary>
    public List<string>? ImageUrls { get; set; }
}
