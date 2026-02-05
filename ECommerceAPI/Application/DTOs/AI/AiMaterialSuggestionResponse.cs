namespace ECommerceAPI.Application.DTOs.AI;

/// <summary>
/// Response gợi ý materials từ AI
/// </summary>
public class AiMaterialSuggestionResponse
{
    /// <summary>
    /// Danh sách materials được gợi ý
    /// </summary>
    public List<MaterialSuggestion> SuggestedMaterials { get; set; } = new();

    /// <summary>
    /// Thời gian xử lý
    /// </summary>
    public int ProcessingTimeMs { get; set; }
}

public class MaterialSuggestion
{
    /// <summary>
    /// Tên material
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>
    /// Độ tin cậy
    /// </summary>
    public decimal ConfidenceScore { get; set; }
}
