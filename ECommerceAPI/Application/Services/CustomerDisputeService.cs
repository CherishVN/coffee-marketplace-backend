using System.Text.Json;
using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Disputes;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class CustomerDisputeService : ICustomerDisputeService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;
    private const int DisputeWindowDays = 7;

    // Terminal statuses where evidence can no longer be updated
    private static readonly DisputeStatus[] FinalStatuses =
    [
        DisputeStatus.Resolved,
        DisputeStatus.Rejected,
        DisputeStatus.Refunded,
        DisputeStatus.Cancelled
    ];

    // Statuses where customer is allowed to cancel:
    //   Pending       — chưa ai xử lý, huỷ thoải mái
    //   WaitingSeller — admin đang chờ seller, customer đã tự dàn xếp được
    //   WaitingCustomer — admin chờ phản hồi thêm từ customer, customer chủ động đóng
    // KHÔNG cho huỷ khi UnderReview vì admin đang bỏ công xử lý
    private static readonly DisputeStatus[] CustomerCancellableStatuses =
    [
        DisputeStatus.Pending,
        DisputeStatus.WaitingSeller,
        DisputeStatus.WaitingCustomer
    ];

    public CustomerDisputeService(ApplicationDbContext context, INotificationService notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    public async Task<CustomerDisputeResponseDto> CreateDisputeAsync(Guid customerId, CreateDisputeDto dto)
    {
        var order = await _context.Orders
            .Include(o => o.Shop)
            .FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.CustomerId == customerId);

        if (order == null)
        {
            return Fail("Không tìm thấy đơn hàng");
        }

        // BR: Order status must be Delivered or Completed
        var allowedStatuses = new short[]
        {
            (short)OrderStatus.Delivered,
            (short)OrderStatus.Completed
        };

        if (!allowedStatuses.Contains(order.Status))
        {
            return Fail("Chỉ có thể khiếu nại đơn hàng đã giao (Delivered) hoặc đã hoàn thành (Completed)");
        }

        // BR: Must be within 7 days after delivery/completion
        var daysSinceUpdate = (DateTime.UtcNow - order.UpdatedAt).TotalDays;
        if (daysSinceUpdate > DisputeWindowDays)
        {
            return Fail($"Đã quá thời hạn khiếu nại ({DisputeWindowDays} ngày kể từ khi đơn được giao/hoàn thành)");
        }

        // BR: One dispute per order
        var existingDispute = await _context.Disputes
            .AnyAsync(d => d.OrderId == dto.OrderId);

        if (existingDispute)
        {
            return Fail("Đơn hàng này đã có khiếu nại");
        }

        // BR: Không được yêu cầu hoàn vượt tổng giá trị đơn hàng
        if (dto.RequestedAmount > order.Total)
        {
            return Fail($"Số tiền yêu cầu không được vượt quá tổng đơn hàng ({order.Total:N0} VND).");
        }

        if (dto.Type == (short)DisputeType.Refund && dto.RequestedAmount <= 0)
        {
            return Fail("Với loại «Hoàn tiền», vui lòng nhập số tiền hoàn lớn hơn 0 (tối đa bằng tổng đơn).");
        }

        var evidenceJson = dto.EvidenceUrls != null && dto.EvidenceUrls.Count > 0
            ? JsonSerializer.Serialize(dto.EvidenceUrls)
            : "[]";

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            OrderId = dto.OrderId,
            CustomerId = customerId,
            ShopId = order.ShopId,
            Type = dto.Type,
            Status = (short)DisputeStatus.Pending,
            Title = dto.Title,
            Reason = dto.Reason,
            EvidenceUrls = evidenceJson,
            RequestedAmount = dto.RequestedAmount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var orderRef = NotificationFormatting.ShortEntityId(order.Id);
        await _notifications.PublishAsync(
            order.Shop.OwnerId,
            nameof(NotificationType.Dispute),
            "Khiếu nại mới",
            $"Khách hàng đã tạo khiếu nại cho đơn #{orderRef}: {dto.Title}",
            "Dispute",
            dispute.Id,
            queueEmail: true);

        return new CustomerDisputeResponseDto
        {
            Success = true,
            Message = "Tạo khiếu nại thành công. Trạng thái: Pending Validation",
            Dispute = MapToDto(dispute, order.Shop.Name)
        };
    }

    public async Task<CustomerDisputeResponseDto> UpdateEvidenceAsync(Guid customerId, Guid disputeId, UpdateEvidenceDto dto)
    {
        var dispute = await _context.Disputes
            .Include(d => d.Shop)
            .FirstOrDefaultAsync(d => d.Id == disputeId && d.CustomerId == customerId);

        if (dispute == null)
        {
            return Fail("Không tìm thấy khiếu nại");
        }

        // BR: Evidence can be updated only before admin final decision
        if (FinalStatuses.Contains((DisputeStatus)dispute.Status))
        {
            return Fail("Không thể cập nhật bằng chứng sau khi admin đã ra quyết định cuối");
        }

        dispute.EvidenceUrls = JsonSerializer.Serialize(dto.EvidenceUrls);
        if (dto.CustomerNote != null)
            dispute.CustomerNote = dto.CustomerNote.Trim();

        // Nếu admin đang chờ customer phản hồi → tự động chuyển về UnderReview sau khi customer submit
        var wasWaitingCustomer = (DisputeStatus)dispute.Status == DisputeStatus.WaitingCustomer;
        if (wasWaitingCustomer)
            dispute.Status = (short)DisputeStatus.UnderReview;

        dispute.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new CustomerDisputeResponseDto
        {
            Success = true,
            Message = wasWaitingCustomer
                ? "Đã gửi phản hồi. Admin sẽ xem xét và liên hệ lại với bạn."
                : "Cập nhật bằng chứng thành công",
            Dispute = MapToDto(dispute, dispute.Shop.Name)
        };
    }

    public async Task<CustomerDisputeListResponseDto> GetMyDisputesAsync(Guid customerId, int page, int pageSize, short? status = null)
    {
        var query = _context.Disputes
            .Include(d => d.Shop)
            .Where(d => d.CustomerId == customerId);

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        var totalCount = await query.CountAsync();

        var disputes = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CustomerDisputeListResponseDto
        {
            Success = true,
            Disputes = disputes.Select(d => MapToDto(d, d.Shop.Name)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerDisputeResponseDto> GetDisputeByIdAsync(Guid customerId, Guid disputeId)
    {
        var dispute = await _context.Disputes
            .Include(d => d.Shop)
            .FirstOrDefaultAsync(d => d.Id == disputeId && d.CustomerId == customerId);

        if (dispute == null)
        {
            return Fail("Không tìm thấy khiếu nại");
        }

        return new CustomerDisputeResponseDto
        {
            Success = true,
            Dispute = MapToDto(dispute, dispute.Shop.Name)
        };
    }

    public async Task<CustomerDisputeResponseDto> CancelDisputeAsync(Guid customerId, Guid disputeId)
    {
        var dispute = await _context.Disputes
            .Include(d => d.Shop)
            .FirstOrDefaultAsync(d => d.Id == disputeId && d.CustomerId == customerId);

        if (dispute == null)
        {
            return Fail("Không tìm thấy khiếu nại");
        }

        if (!CustomerCancellableStatuses.Contains((DisputeStatus)dispute.Status))
        {
            return Fail("Không thể hủy khiếu nại ở trạng thái này. " +
                "Chỉ được hủy khi đang Chờ xử lý, Chờ seller phản hồi hoặc Chờ phản hồi từ bạn.");
        }

        dispute.Status = (short)DisputeStatus.Cancelled;
        dispute.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var ordRef = NotificationFormatting.ShortEntityId(dispute.OrderId);
        await _notifications.PublishAsync(
            dispute.Shop.OwnerId,
            nameof(NotificationType.Dispute),
            "Khiếu nại đã hủy",
            $"Khách hàng đã hủy khiếu nại liên quan đơn #{ordRef}.",
            "Dispute",
            dispute.Id,
            queueEmail: true);

        return new CustomerDisputeResponseDto
        {
            Success = true,
            Message = "Đã hủy khiếu nại",
            Dispute = MapToDto(dispute, dispute.Shop.Name)
        };
    }

    private static CustomerDisputeDto MapToDto(Dispute dispute, string shopName)
    {
        var evidenceUrls = TryDeserializeUrls(dispute.EvidenceUrls);
        var sellerEvidenceUrls = TryDeserializeUrls(dispute.SellerEvidenceUrls);
        var isFinal = FinalStatuses.Contains((DisputeStatus)dispute.Status);

        return new CustomerDisputeDto
        {
            Id = dispute.Id,
            OrderId = dispute.OrderId,
            ShopId = dispute.ShopId,
            ShopName = shopName,
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
            CanUpdateEvidence = !isFinal,
            CustomerNote = dispute.CustomerNote
        };
    }

    private static List<string> TryDeserializeUrls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static CustomerDisputeResponseDto Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
