using ECommerceAPI.Application.DTOs.Chat;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class ConversationService : IConversationService
{
    private readonly ApplicationDbContext _context;

    public ConversationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResponse<ConversationDto>> StartOrGetConversationAsync(Guid buyerId, StartConversationDto dto)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == dto.ShopId && s.VerificationStatus == 1); // 1 = approved

        if (shop == null)
            return new ServiceResponse<ConversationDto> { Success = false, Message = "Shop không tồn tại hoặc chưa được duyệt" };

        if (shop.OwnerId == buyerId)
            return new ServiceResponse<ConversationDto> { Success = false, Message = "Không thể tự nhắn tin cho chính mình" };

        // Tìm conversation đã có (cùng buyer + shop + order nếu có)
        var existing = await _context.Conversations
            .Include(c => c.Shop)
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
            .FirstOrDefaultAsync(c =>
                c.BuyerId == buyerId &&
                c.ShopId == dto.ShopId &&
                c.OrderId == dto.OrderId);

        if (existing != null)
        {
            // Nếu có tin nhắn đầu tiên thì gửi luôn
            if (!string.IsNullOrWhiteSpace(dto.FirstMessage))
            {
                var msg = new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = existing.Id,
                    SenderId = buyerId,
                    MessageType = "text",
                    Content = dto.FirstMessage.Trim(),
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.Messages.AddAsync(msg);
                await _context.SaveChangesAsync();
            }

            return new ServiceResponse<ConversationDto>
            {
                Success = true,
                Data = await MapConversationDtoAsync(existing, buyerId)
            };
        }

        // Tạo conversation mới
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            BuyerId = buyerId,
            SellerId = shop.OwnerId,
            ShopId = dto.ShopId,
            OrderId = dto.OrderId,
            CreatedAt = DateTime.UtcNow
        };
        await _context.Conversations.AddAsync(conversation);

        // Gửi tin nhắn đầu tiên nếu có
        if (!string.IsNullOrWhiteSpace(dto.FirstMessage))
        {
            var firstMsg = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderId = buyerId,
                MessageType = "text",
                Content = dto.FirstMessage.Trim(),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            await _context.Messages.AddAsync(firstMsg);
        }

        await _context.SaveChangesAsync();

        // Load lại để có đủ navigation properties
        var created = await _context.Conversations
            .Include(c => c.Shop)
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
            .FirstAsync(c => c.Id == conversation.Id);

        return new ServiceResponse<ConversationDto>
        {
            Success = true,
            Data = await MapConversationDtoAsync(created, buyerId)
        };
    }

    public async Task<ServiceResponse<List<ConversationDto>>> GetMyConversationsAsync(Guid userId)
    {
        var conversations = await _context.Conversations
            .Include(c => c.Shop)
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
            .Where(c => c.BuyerId == userId || c.SellerId == userId)
            .OrderByDescending(c => c.Messages
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.CreatedAt)
                .FirstOrDefault())
            .ToListAsync();

        var result = new List<ConversationDto>();
        foreach (var c in conversations)
            result.Add(await MapConversationDtoAsync(c, userId));

        return new ServiceResponse<List<ConversationDto>> { Success = true, Data = result };
    }

    public async Task<ServiceResponse<ConversationDetailDto>> GetConversationMessagesAsync(
        Guid userId, Guid conversationId, int page, int pageSize)
    {
        var conversation = await _context.Conversations
            .Include(c => c.Shop)
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .FirstOrDefaultAsync(c =>
                c.Id == conversationId &&
                (c.BuyerId == userId || c.SellerId == userId));

        if (conversation == null)
            return new ServiceResponse<ConversationDetailDto>
            {
                Success = false,
                Message = "Không tìm thấy cuộc trò chuyện"
            };

        var totalMessages = await _context.Messages
            .CountAsync(m => m.ConversationId == conversationId);

        var messages = await _context.Messages
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Đánh dấu đã đọc các tin nhắn của đối phương
        var unread = messages.Where(m => m.SenderId != userId && !m.IsRead).ToList();
        if (unread.Any())
        {
            unread.ForEach(m => m.IsRead = true);
            await _context.SaveChangesAsync();
        }

        var conversationDto = await MapConversationDtoAsync(conversation, userId);
        conversationDto.UnreadCount = 0;

        return new ServiceResponse<ConversationDetailDto>
        {
            Success = true,
            Data = new ConversationDetailDto
            {
                Conversation = conversationDto,
                Messages = messages.Select(m => MapMessageDto(m, conversation)).ToList(),
                TotalMessages = totalMessages,
                Page = page,
                PageSize = pageSize
            }
        };
    }

    public async Task<ServiceResponse<MessageDto>> SendMessageAsync(
        Guid senderId, Guid conversationId, SendMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return new ServiceResponse<MessageDto> { Success = false, Message = "Nội dung tin nhắn không được để trống" };

        var conversation = await _context.Conversations
            .Include(c => c.Shop)
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .FirstOrDefaultAsync(c =>
                c.Id == conversationId &&
                (c.BuyerId == senderId || c.SellerId == senderId));

        if (conversation == null)
            return new ServiceResponse<MessageDto>
            {
                Success = false,
                Message = "Không tìm thấy cuộc trò chuyện hoặc bạn không có quyền truy cập"
            };

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            MessageType = dto.MessageType ?? "text",
            Content = dto.Content.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Messages.AddAsync(message);
        await _context.SaveChangesAsync();

        // Load sender để lấy tên
        await _context.Entry(message).Reference(m => m.Sender).LoadAsync();

        return new ServiceResponse<MessageDto>
        {
            Success = true,
            Data = MapMessageDto(message, conversation)
        };
    }

    public async Task<ServiceResponse> MarkAsReadAsync(Guid userId, Guid conversationId)
    {
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c =>
                c.Id == conversationId &&
                (c.BuyerId == userId || c.SellerId == userId));

        if (conversation == null)
            return new ServiceResponse { Success = false, Message = "Không tìm thấy cuộc trò chuyện" };

        var unreadMessages = await _context.Messages
            .Where(m =>
                m.ConversationId == conversationId &&
                m.SenderId != userId &&
                !m.IsRead)
            .ToListAsync();

        if (unreadMessages.Any())
        {
            unreadMessages.ForEach(m => m.IsRead = true);
            await _context.SaveChangesAsync();
        }

        return new ServiceResponse { Success = true, Message = $"Đã đánh dấu {unreadMessages.Count} tin nhắn đã đọc" };
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<ConversationDto> MapConversationDtoAsync(Conversation c, Guid currentUserId)
    {
        var unreadCount = await _context.Messages
            .CountAsync(m =>
                m.ConversationId == c.Id &&
                m.SenderId != currentUserId &&
                !m.IsRead);

        var lastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();

        return new ConversationDto
        {
            Id = c.Id,
            ShopId = c.ShopId,
            ShopName = c.Shop?.Name ?? string.Empty,
            ShopLogoUrl = c.Shop?.LogoUrl,
            BuyerId = c.BuyerId,
            BuyerName = c.Buyer?.FullName ?? string.Empty,
            SellerId = c.SellerId,
            OrderId = c.OrderId,
            UnreadCount = unreadCount,
            CreatedAt = c.CreatedAt,
            LastMessage = lastMessage == null ? null : new MessageDto
            {
                Id = lastMessage.Id,
                ConversationId = lastMessage.ConversationId,
                SenderId = lastMessage.SenderId,
                SenderName = lastMessage.Sender?.FullName ?? string.Empty,
                SenderRole = lastMessage.SenderId == c.BuyerId ? "buyer" : "seller",
                MessageType = lastMessage.MessageType,
                Content = lastMessage.Content,
                IsRead = lastMessage.IsRead,
                CreatedAt = lastMessage.CreatedAt
            }
        };
    }

    private static MessageDto MapMessageDto(Message m, Conversation c)
    {
        return new MessageDto
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            SenderId = m.SenderId,
            SenderName = m.Sender?.FullName ?? string.Empty,
            SenderRole = m.SenderId == c.BuyerId ? "buyer" : "seller",
            MessageType = m.MessageType,
            Content = m.Content,
            IsRead = m.IsRead,
            CreatedAt = m.CreatedAt
        };
    }
}
