namespace ECommerceAPI.Application.Interfaces;

/// <summary>
/// AI suggestion service dùng API key riêng cho Seller.
/// Inject interface này vào các Seller controller thay vì IAiSuggestionService.
/// </summary>
public interface ISellerAiSuggestionService : IAiSuggestionService { }
