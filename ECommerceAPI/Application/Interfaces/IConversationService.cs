using ECommerceAPI.Application.DTOs.Chat;

namespace ECommerceAPI.Application.Interfaces;

public interface IConversationService
{
    /// <summary>Tạo mới hoặc lấy conversation đã có giữa buyer và shop</summary>
    Task<ServiceResponse<ConversationDto>> StartOrGetConversationAsync(Guid buyerId, StartConversationDto dto);

    /// <summary>Lấy danh sách tất cả conversations của user (cả buyer lẫn seller)</summary>
    Task<ServiceResponse<List<ConversationDto>>> GetMyConversationsAsync(Guid userId);

    /// <summary>Lấy chi tiết conversation + lịch sử tin nhắn (phân trang)</summary>
    Task<ServiceResponse<ConversationDetailDto>> GetConversationMessagesAsync(Guid userId, Guid conversationId, int page, int pageSize);

    /// <summary>Gửi tin nhắn trong conversation</summary>
    Task<ServiceResponse<MessageDto>> SendMessageAsync(Guid senderId, Guid conversationId, SendMessageDto dto);

    /// <summary>Đánh dấu tất cả tin nhắn trong conversation là đã đọc</summary>
    Task<ServiceResponse> MarkAsReadAsync(Guid userId, Guid conversationId);

    Task<ServiceResponse> SetConversationMutedAsync(Guid userId, Guid conversationId, bool muted);

    /// <summary>Ẩn cuộc trò chuyện khỏi danh sách của user (tin mới từ đối phương sẽ hiện lại).</summary>
    Task<ServiceResponse> HideConversationAsync(Guid userId, Guid conversationId);
}
