using ECommerceAI.DTOs.Chat;

namespace ECommerceAI.Services.Interfaces;

public interface IAiChatService
{
    Task<SessionResponseDto> GetOrCreateSessionAsync(Guid userId);
    Task<SendMessageResponseDto> SendMessageAsync(Guid sessionId, Guid userId, string message);
    Task<ConfirmOrderResponseDto> ConfirmOrderAsync(Guid sessionId, Guid userId, Guid cartId, Guid shippingAddressId);
    Task<SessionResponseDto> GetHistoryAsync(Guid sessionId, Guid userId);
}
