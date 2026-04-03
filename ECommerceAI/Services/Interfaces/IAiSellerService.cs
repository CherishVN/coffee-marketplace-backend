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
    /// Lấy lịch sử gợi ý tags của seller (không bao gồm các bản ghi đang pending).
    /// </summary>
    Task<TagSuggestionLogResponse> GetTagSuggestionLogsAsync(Guid sellerId, int page, int pageSize);

    Task<AnalyzeImageResponseDto> AnalyzeImageAsync(AnalyzeImageRequestDto request, Guid sellerId);
}
