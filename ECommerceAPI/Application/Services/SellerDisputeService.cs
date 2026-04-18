using System.Text.Json;
using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Disputes;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class SellerDisputeService : ISellerDisputeService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;

    private static readonly DisputeStatus[] FinalStatuses =
    [
        DisputeStatus.Resolved,
        DisputeStatus.Rejected,
        DisputeStatus.Refunded,
        DisputeStatus.Cancelled
    ];

    public SellerDisputeService(ApplicationDbContext context, INotificationService notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    public async Task<SellerDisputeListResponseDto> GetShopDisputesAsync(
        Guid sellerId, int page, int pageSize, short? status = null, short? type = null)
    {
        // Xác định shop của seller
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == sellerId);

        if (shop == null)
        {
            return new SellerDisputeListResponseDto
            {
                Success = false,
                Message = "Bạn chưa có shop"
            };
        }

        var query = _context.Disputes
            .Include(d => d.Customer)
            .Where(d => d.ShopId == shop.Id);

        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        if (type.HasValue)
            query = query.Where(d => d.Type == type.Value);

        var totalCount = await query.CountAsync();

        var disputes = await query
            .Include(d => d.DisputeOrderItems)
            .ThenInclude(x => x.OrderItem)
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new SellerDisputeListResponseDto
        {
            Success = true,
            Disputes = disputes.Select(d => MapToDto(d, d.Customer.FullName ?? d.Customer.Phone ?? "Khách hàng")).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<SellerDisputeResponseDto> GetDisputeByIdAsync(Guid sellerId, Guid disputeId)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == sellerId);

        if (shop == null)
            return Fail("Bạn chưa có shop");

        var dispute = await _context.Disputes
            .Include(d => d.Customer)
            .Include(d => d.DisputeOrderItems)
            .ThenInclude(x => x.OrderItem)
            .FirstOrDefaultAsync(d => d.Id == disputeId && d.ShopId == shop.Id);

        if (dispute == null)
            return Fail("Không tìm thấy tranh chấp");

        return new SellerDisputeResponseDto
        {
            Success = true,
            Dispute = MapToDto(dispute, dispute.Customer.FullName ?? dispute.Customer.Phone ?? "Khách hàng")
        };
    }

    public async Task<SellerDisputeResponseDto> RespondToDisputeAsync(
        Guid sellerId, Guid disputeId, SellerRespondDisputeDto dto)
    {
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.OwnerId == sellerId);

        if (shop == null)
            return Fail("Bạn chưa có shop");

        var dispute = await _context.Disputes
            .Include(d => d.Customer)
            .Include(d => d.DisputeOrderItems)
            .ThenInclude(x => x.OrderItem)
            .FirstOrDefaultAsync(d => d.Id == disputeId && d.ShopId == shop.Id);

        if (dispute == null)
            return Fail("Không tìm thấy tranh chấp");

        if (FinalStatuses.Contains((DisputeStatus)dispute.Status))
            return Fail("Tranh chấp này đã được xử lý, không thể phản hồi thêm");

        // Cập nhật phản hồi của seller
        dispute.SellerResponse = dto.Response;
        dispute.SellerRespondedAt = DateTime.UtcNow;
        dispute.UpdatedAt = DateTime.UtcNow;

        if (dto.EvidenceUrls != null && dto.EvidenceUrls.Count > 0)
            dispute.SellerEvidenceUrls = JsonSerializer.Serialize(dto.EvidenceUrls);

        // Sau khi seller phản hồi → trả về UnderReview để admin xem xét quyết định
        if (dispute.Status == (short)DisputeStatus.WaitingSeller ||
            dispute.Status == (short)DisputeStatus.Pending ||
            dispute.Status == (short)DisputeStatus.UnderReview ||
            dispute.Status == (short)DisputeStatus.WaitingCustomer)
        {
            dispute.Status = (short)DisputeStatus.UnderReview;
        }

        await _context.SaveChangesAsync();

        var orderRef = NotificationFormatting.ShortEntityId(dispute.OrderId);
        // Thông báo cho customer biết seller đã có phản hồi
        await _notifications.PublishAsync(
            dispute.CustomerId,
            nameof(NotificationType.Dispute),
            "Shop đã phản hồi khiếu nại",
            $"Shop đã phản hồi khiếu nại của bạn liên quan đơn #{orderRef}. Admin đang xem xét.",
            "Dispute",
            dispute.Id,
            queueEmail: true);

        await _notifications.PublishToUsersWithRoleAsync(
            "admin",
            nameof(NotificationType.Dispute),
            "Shop đã phản hồi khiếu nại",
            $"Đơn #{orderRef}: người bán đã gửi phản hồi — cần xem xét (khiếu nại «{dispute.Title}»).",
            "Dispute",
            dispute.Id,
            queueEmail: false);

        return new SellerDisputeResponseDto
        {
            Success = true,
            Message = "Phản hồi thành công",
            Dispute = MapToDto(dispute, dispute.Customer.FullName ?? dispute.Customer.Phone ?? "Khách hàng")
        };
    }

    private static SellerDisputeDto MapToDto(Domain.Entities.Dispute dispute, string customerName)
    {
        var evidenceUrls = TryDeserializeUrls(dispute.EvidenceUrls);
        var sellerEvidenceUrls = TryDeserializeUrls(dispute.SellerEvidenceUrls);
        var isFinal = FinalStatuses.Contains((DisputeStatus)dispute.Status);

        return new SellerDisputeDto
        {
            Id = dispute.Id,
            OrderId = dispute.OrderId,
            CustomerId = dispute.CustomerId,
            CustomerName = customerName,
            ShopId = dispute.ShopId,
            Type = dispute.Type,
            Status = dispute.Status,
            Title = dispute.Title,
            Reason = dispute.Reason,
            RequestedAmount = dispute.RequestedAmount,
            ApprovedAmount = dispute.ApprovedAmount,
            Resolution = dispute.Resolution,
            EvidenceUrls = evidenceUrls,
            SellerEvidenceUrls = sellerEvidenceUrls,
            SellerResponse = dispute.SellerResponse,
            SellerRespondedAt = dispute.SellerRespondedAt,
            CreatedAt = dispute.CreatedAt,
            UpdatedAt = dispute.UpdatedAt,
            CanRespond = !isFinal,
            CustomerNote = dispute.CustomerNote,
            AdminNote = dispute.AdminNote,
            AffectedItems = MapSellerAffectedItems(dispute)
        };
    }

    private static List<DisputeAffectedItemDto> MapSellerAffectedItems(Domain.Entities.Dispute dispute)
    {
        if (dispute.DisputeOrderItems == null || dispute.DisputeOrderItems.Count == 0)
            return new List<DisputeAffectedItemDto>();

        return dispute.DisputeOrderItems
            .OrderBy(x => x.OrderItem?.ProductName)
            .Select(r => new DisputeAffectedItemDto
            {
                OrderItemId = r.OrderItemId,
                ProductName = r.OrderItem?.ProductName ?? "",
                Quantity = r.Quantity,
                UnitPrice = r.UnitPriceSnapshot,
                LineTotal = r.LineSnapshotTotal
            })
            .ToList();
    }

    private static List<string> TryDeserializeUrls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new(); }
    }

    private static SellerDisputeResponseDto Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
