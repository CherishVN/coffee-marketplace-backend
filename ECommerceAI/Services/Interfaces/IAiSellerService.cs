using ECommerceAI.DTOs.Seller;

namespace ECommerceAI.Services.Interfaces;

public interface IAiSellerService
{
    Task<SuggestCategoryResponseDto> SuggestCategoryAsync(SuggestCategoryRequestDto request, Guid sellerId);
    Task<SuggestTagsResponseDto> SuggestTagsAsync(SuggestTagsRequestDto request, Guid sellerId);
    Task<SuggestMaterialsResponseDto> SuggestMaterialsAsync(SuggestMaterialsRequestDto request, Guid sellerId);

   
    Task<bool> SaveTagSuggestionFeedbackAsync(SaveSuggestionFeedbackDto dto, Guid sellerId);

  
    Task<bool> SaveMaterialSuggestionFeedbackAsync(SaveMaterialFeedbackDto dto, Guid sellerId);

  
    Task<TagSuggestionLogResponse> GetTagSuggestionLogsAsync(Guid sellerId, int page, int pageSize);

  
    Task<MaterialSuggestionLogResponse> GetMaterialSuggestionLogsAsync(Guid sellerId, int page, int pageSize);

    Task<AnalyzeImageResponseDto> AnalyzeImageAsync(AnalyzeImageRequestDto request, Guid sellerId);

   
    Task<AnalyzeProductResponseDto> AnalyzeProductAsync(AnalyzeProductRequestDto request, Guid sellerId);

 
    Task<bool> CommitProductAiTagSessionAsync(CommitProductAiTagSessionDto dto, Guid sellerId);
}
