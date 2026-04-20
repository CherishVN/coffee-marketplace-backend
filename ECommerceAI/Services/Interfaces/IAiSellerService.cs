using ECommerceAI.DTOs.Seller;

namespace ECommerceAI.Services.Interfaces;

public interface IAiSellerService
{
    Task<SuggestCategoryResponseDto> SuggestCategoryAsync(SuggestCategoryRequestDto request, Guid sellerId);
    Task<SuggestTagsResponseDto> SuggestTagsAsync(SuggestTagsRequestDto request, Guid sellerId);
    Task<SuggestMaterialsResponseDto> SuggestMaterialsAsync(SuggestMaterialsRequestDto request, Guid sellerId);

    /// <summary>
    /// Lưu phản hồi của seller sau khi chọn tags từ gợi ý AI.
    /// Chỉ hoạt động khi suggest-tags đã được gọi kèm productId (logId có giá trị).
    /// </summary>
    Task<bool> SaveTagSuggestionFeedbackAsync(SaveSuggestionFeedbackDto dto, Guid sellerId);

    /// <summary>
    /// Lưu phản hồi của seller sau khi chọn materials từ gợi ý AI.
    /// Chỉ hoạt động khi suggest-materials đã được gọi kèm productId (logId có giá trị).
    /// </summary>
    Task<bool> SaveMaterialSuggestionFeedbackAsync(SaveMaterialFeedbackDto dto, Guid sellerId);

    /// <summary>
    /// Lấy lịch sử gợi ý tags của seller (không bao gồm các bản ghi đang pending).
    /// </summary>
    Task<TagSuggestionLogResponse> GetTagSuggestionLogsAsync(Guid sellerId, int page, int pageSize);

    /// <summary>
    /// Lấy lịch sử gợi ý chất liệu của seller (không bao gồm pending).
    /// </summary>
    Task<MaterialSuggestionLogResponse> GetMaterialSuggestionLogsAsync(Guid sellerId, int page, int pageSize);

    Task<AnalyzeImageResponseDto> AnalyzeImageAsync(AnalyzeImageRequestDto request, Guid sellerId);

    /// <summary>
    /// Phân tích sản phẩm (text-only) — trả về category + tags + materials trong 1 lần gọi Gemini.
    /// Kết quả được post-validate: chỉ trả về IDs thực sự tồn tại trong DB.
    /// </summary>
    Task<AnalyzeProductResponseDto> AnalyzeProductAsync(AnalyzeProductRequestDto request, Guid sellerId);

    /// <summary>
    /// Lưu lịch sử gợi ý tag sau khi tạo sản phẩm (kèm phân tích AI trên form).
    /// </summary>
    Task<bool> CommitProductAiTagSessionAsync(CommitProductAiTagSessionDto dto, Guid sellerId);
}
