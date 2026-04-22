namespace ECommerceAPI.Application.Interfaces;


public interface ISellerProductContentAlignmentClient
{
   
    Task<(bool Ok, string? ErrorMessage)> ValidateContentMatchesCategoryAsync(
        string? authorizationHeader,
        string title,
        string? description,
        IReadOnlyList<string> imageUrls,
        long expectedCategoryId,
        CancellationToken cancellationToken = default);
}
