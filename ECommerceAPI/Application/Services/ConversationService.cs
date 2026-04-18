using ECommerceAPI.Application.DTOs.Chat;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Hubs;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace ECommerceAPI.Application.Services;

public class ConversationService : IConversationService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserAuthEmailResolver _authResolver;
    private readonly INotificationService _notifications;
    private readonly IHubContext<OrderTrackingHub> _hubContext;

    public ConversationService(
        ApplicationDbContext context,
        IUserAuthEmailResolver authResolver,
        INotificationService notifications,
        IHubContext<OrderTrackingHub> hubContext)
    {
        _context = context;
        _authResolver = authResolver;
        _notifications = notifications;
        _hubContext = hubContext;
    }

    public async Task<ServiceResponse<ConversationDto>> StartOrGetConversationAsync(Guid buyerId, StartConversationDto dto)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == dto.ShopId && s.VerificationStatus == 1); // 1 = approved

        if (shop == null)
            return new ServiceResponse<ConversationDto> { Success = false, Message = "Shop không tồn tại hoặc chưa được duyệt" };

        if (shop.OwnerId == buyerId)
            return new ServiceResponse<ConversationDto> { Success = false, Message = "Không thể tự nhắn tin cho chính mình" };

        var chatProduct = await ResolveChatProductForStartAsync(dto.ShopId, dto.ProductId);
        if (dto.ProductId.HasValue && chatProduct == null)
            return new ServiceResponse<ConversationDto> { Success = false, Message = "Sản phẩm không tồn tại hoặc không thuộc shop này" };

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
            await UnhideForUserAsync(buyerId, existing.Id);

            if (chatProduct != null && existing.ProductId != chatProduct.Id)
            {
                existing.ProductId = chatProduct.Id;
                await _context.SaveChangesAsync();
            }

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

            var existingLoaded = await LoadConversationGraphAsync(existing.Id);
            return new ServiceResponse<ConversationDto>
            {
                Success = true,
                Data = await MapConversationDtoAsync(existingLoaded, buyerId)
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
            ProductId = chatProduct?.Id,
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

        var created = await LoadConversationGraphAsync(conversation.Id);

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
            .Include(c => c.Product)
                .ThenInclude(p => p!.ProductImages)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
            .Where(c => c.BuyerId == userId || c.SellerId == userId)
            .Where(c => !_context.ConversationUserPreferences.Any(p =>
                p.ConversationId == c.Id &&
                p.UserId == userId &&
                p.HiddenAt != null))
            .OrderByDescending(c => c.Messages
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.CreatedAt)
                .FirstOrDefault())
            .ToListAsync();

        var ids = conversations.Select(c => c.Id).ToList();
        var prefRows = await _context.ConversationUserPreferences.AsNoTracking()
            .Where(p => p.UserId == userId && ids.Contains(p.ConversationId))
            .ToListAsync();
        var prefByConv = prefRows.ToDictionary(p => p.ConversationId);

        var result = new List<ConversationDto>();
        var avatarCache = new Dictionary<Guid, string?>();
        foreach (var c in conversations)
            result.Add(await MapConversationDtoAsync(c, userId, avatarCache, prefByConv));

        return new ServiceResponse<List<ConversationDto>> { Success = true, Data = result };
    }

    public async Task<ServiceResponse<ConversationDetailDto>> GetConversationMessagesAsync(
        Guid userId, Guid conversationId, int page, int pageSize)
    {
        var conversation = await _context.Conversations
            .Include(c => c.Shop)
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .Include(c => c.Product)
                .ThenInclude(p => p!.ProductImages)
            .FirstOrDefaultAsync(c =>
                c.Id == conversationId &&
                (c.BuyerId == userId || c.SellerId == userId));

        if (conversation == null)
            return new ServiceResponse<ConversationDetailDto>
            {
                Success = false,
                Message = "Không tìm thấy cuộc trò chuyện"
            };

        await UnhideForUserAsync(userId, conversationId);

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

        var recipientId = senderId == conversation.BuyerId ? conversation.SellerId : conversation.BuyerId;
        await UnhideForUserAsync(recipientId, conversationId);

        // Load sender để lấy tên
        await _context.Entry(message).Reference(m => m.Sender).LoadAsync();

        // Load latest conversation state (including newest message) for realtime push.
        var conversationForPush = await _context.Conversations
            .Include(c => c.Shop)
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .Include(c => c.Product)
                .ThenInclude(p => p!.ProductImages)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
            .FirstAsync(c => c.Id == conversationId);

        var buyerConversation = await MapConversationDtoAsync(conversationForPush, conversationForPush.BuyerId);
        var sellerConversation = await MapConversationDtoAsync(conversationForPush, conversationForPush.SellerId);
        var messageDto = MapMessageDto(message, conversation);

        var buyerGroup = OrderTrackingHub.GetUserGroupName(conversationForPush.BuyerId);
        var sellerGroup = OrderTrackingHub.GetUserGroupName(conversationForPush.SellerId);

        var recipientMuted = await _context.ConversationUserPreferences.AsNoTracking()
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == recipientId && p.IsMuted);

        if (!recipientMuted)
        {
            var senderName = message.Sender?.FullName ?? "Người dùng";
            var preview = (dto.MessageType ?? "text") == "image"
                ? "[Hình ảnh]"
                : (message.Content.Length > 160 ? message.Content[..160] + "…" : message.Content);

            await _notifications.PublishAsync(
                recipientId,
                "chat_message",
                $"Tin nhắn từ {senderName}",
                preview,
                "conversation",
                conversationId);
        }

        await _hubContext.Clients.Group(buyerGroup).SendAsync("ChatMessageReceived", new
        {
            ConversationId = conversationId,
            Message = messageDto
        });
        await _hubContext.Clients.Group(sellerGroup).SendAsync("ChatMessageReceived", new
        {
            ConversationId = conversationId,
            Message = messageDto
        });

        await _hubContext.Clients.Group(buyerGroup).SendAsync("ConversationUpdated", buyerConversation);
        await _hubContext.Clients.Group(sellerGroup).SendAsync("ConversationUpdated", sellerConversation);

        return new ServiceResponse<MessageDto>
        {
            Success = true,
            Data = messageDto
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

    public async Task<ServiceResponse> SetConversationMutedAsync(Guid userId, Guid conversationId, bool muted)
    {
        var conv = await _context.Conversations
            .FirstOrDefaultAsync(c =>
                c.Id == conversationId &&
                (c.BuyerId == userId || c.SellerId == userId));

        if (conv == null)
            return new ServiceResponse { Success = false, Message = "Không tìm thấy cuộc trò chuyện" };

        var pref = await _context.ConversationUserPreferences
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (pref == null)
        {
            await _context.ConversationUserPreferences.AddAsync(new ConversationUserPreference
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                UserId = userId,
                IsMuted = muted,
                HiddenAt = null
            });
        }
        else
        {
            pref.IsMuted = muted;
        }

        await _context.SaveChangesAsync();
        return new ServiceResponse { Success = true, Message = muted ? "Đã tắt thông báo" : "Đã bật thông báo" };
    }

    public async Task<ServiceResponse> HideConversationAsync(Guid userId, Guid conversationId)
    {
        var conv = await _context.Conversations
            .FirstOrDefaultAsync(c =>
                c.Id == conversationId &&
                (c.BuyerId == userId || c.SellerId == userId));

        if (conv == null)
            return new ServiceResponse { Success = false, Message = "Không tìm thấy cuộc trò chuyện" };

        var pref = await _context.ConversationUserPreferences
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);
        var now = DateTime.UtcNow;

        if (pref == null)
        {
            await _context.ConversationUserPreferences.AddAsync(new ConversationUserPreference
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                UserId = userId,
                IsMuted = false,
                HiddenAt = now
            });
        }
        else
        {
            pref.HiddenAt = now;
        }

        await _context.SaveChangesAsync();
        return new ServiceResponse { Success = true, Message = "Đã ẩn cuộc trò chuyện" };
    }

    private async Task UnhideForUserAsync(Guid userId, Guid conversationId)
    {
        var pref = await _context.ConversationUserPreferences
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (pref?.HiddenAt == null)
            return;

        pref.HiddenAt = null;
        await _context.SaveChangesAsync();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<Conversation> LoadConversationGraphAsync(Guid id)
    {
        return await _context.Conversations
            .Include(c => c.Shop)
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .Include(c => c.Product)
                .ThenInclude(p => p!.ProductImages)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
            .FirstAsync(c => c.Id == id);
    }

    private async Task<Product?> ResolveChatProductForStartAsync(Guid shopId, Guid? productId)
    {
        if (!productId.HasValue) return null;
        return await _context.Products
            .Include(p => p.ProductImages)
            .FirstOrDefaultAsync(p =>
                p.Id == productId.Value &&
                p.ShopId == shopId &&
                p.Status == (short)ProductStatus.Active);
    }

    private static ChatProductContextDto? MapProductContext(Product? p)
    {
        if (p == null) return null;
        var img = p.ProductImages
            .OrderBy(i => i.SortOrder)
            .FirstOrDefault()?.ImageUrl;
        return new ChatProductContextDto
        {
            ProductId = p.Id,
            Slug = p.Slug,
            Name = p.Name,
            ImageUrl = img,
            Price = p.BasePrice
        };
    }

    private async Task<ConversationDto> MapConversationDtoAsync(
        Conversation c,
        Guid currentUserId,
        Dictionary<Guid, string?>? avatarCache = null,
        IReadOnlyDictionary<Guid, ConversationUserPreference>? prefByConv = null)
    {
        var unreadCount = await _context.Messages
            .CountAsync(m =>
                m.ConversationId == c.Id &&
                m.SenderId != currentUserId &&
                !m.IsRead);

        var lastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
        string? buyerAvatarUrl;

        if (avatarCache != null && avatarCache.TryGetValue(c.BuyerId, out var cachedAvatar))
        {
            buyerAvatarUrl = cachedAvatar;
        }
        else
        {
            buyerAvatarUrl = await _authResolver.GetAvatarUrlByUserIdAsync(c.BuyerId);
            avatarCache?.TryAdd(c.BuyerId, buyerAvatarUrl);
        }

        ConversationUserPreference? pr = null;
        if (prefByConv != null && prefByConv.TryGetValue(c.Id, out var pRow))
            pr = pRow;
        else
            pr = await _context.ConversationUserPreferences.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ConversationId == c.Id && x.UserId == currentUserId);

        var productCtx = MapProductContext(c.Product);
        if (productCtx == null && c.ProductId.HasValue)
        {
            var p = await _context.Products.AsNoTracking()
                .Include(x => x.ProductImages)
                .FirstOrDefaultAsync(x => x.Id == c.ProductId);
            productCtx = MapProductContext(p);
        }

        return new ConversationDto
        {
            Id = c.Id,
            ShopId = c.ShopId,
            ShopName = c.Shop?.Name ?? string.Empty,
            ShopLogoUrl = c.Shop?.LogoUrl,
            BuyerId = c.BuyerId,
            BuyerName = c.Buyer?.FullName ?? string.Empty,
            BuyerAvatarUrl = buyerAvatarUrl,
            SellerId = c.SellerId,
            OrderId = c.OrderId,
            ProductContext = productCtx,
            IsMuted = pr?.IsMuted ?? false,
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
