namespace ECommerceAPI.Application.DTOs.AI;

/// <summary>
/// Request gợi ý chất liệu cho sản phẩm
/// </summary>
public class AiMaterialSuggestionRequest
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
    /// Category ID
    /// </summary>
    public long? CategoryId { get; set; }

    /// <summary>
    /// URLs hình ảnh
    /// </summary>
    public List<string>? ImageUrls { get; set; }
}
