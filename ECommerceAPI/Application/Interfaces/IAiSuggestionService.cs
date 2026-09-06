using ECommerceAPI.Application.DTOs.AI;

namespace ECommerceAPI.Application.Interfaces;

/// <summary>
/// Interface cho việc gọi AI Microservice để lấy gợi ý
/// </summary>
public interface IAiSuggestionService
{
    /// <summary>
    /// Gọi AI service để gợi ý category cho sản phẩm
    /// </summary>
    Task<AiCategorySuggestionResponse?> SuggestCategoryAsync(
        AiCategorySuggestionRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gọi AI service để gợi ý tags cho sản phẩm
    /// </summary>
    Task<AiTagSuggestionResponse?> SuggestTagsAsync(
        AiTagSuggestionRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gọi AI service để gợi ý materials cho sản phẩm
    /// </summary>
    Task<AiMaterialSuggestionResponse?> SuggestMaterialsAsync(
        AiMaterialSuggestionRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra AI service có hoạt động không
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
