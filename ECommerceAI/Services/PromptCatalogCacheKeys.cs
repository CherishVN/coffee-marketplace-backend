namespace ECommerceAI.Services;

/// <summary>Key dùng chung cho IMemoryCache trong <see cref="AiSellerService"/> — phải xóa khi categories/tags/materials thay đổi.</summary>
public static class PromptCatalogCacheKeys
{
    public const string Candidates = "PromptCandidates";
}
