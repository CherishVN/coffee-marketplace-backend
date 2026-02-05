namespace ECommerceAPI.Domain.Enums;

/// <summary>
/// Hành động của seller với gợi ý AI
/// </summary>
public enum AiSuggestionAction
{
    /// <summary>
    /// Chấp nhận gợi ý
    /// </summary>
    Accepted,
    
    /// <summary>
    /// Chỉnh sửa gợi ý
    /// </summary>
    Modified,
    
    /// <summary>
    /// Từ chối/Ghi đè gợi ý
    /// </summary>
    Rejected
}
