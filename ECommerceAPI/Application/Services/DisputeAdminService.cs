using System.Text.Json;
using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.DTOs.Disputes;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class DisputeAdminService : IDisputeAdminService
{
    private static readonly DisputeStatus[] FinalStatuses =
    [
        DisputeStatus.Resolved,
        DisputeStatus.Rejected,
        DisputeStatus.Refunded,
        DisputeStatus.Cancelled
    ];

    private readonly ApplicationDbContext _context;
    private readonly ILogger<DisputeAdminService> _logger;
    private readonly INotificationService _notifications;
    private readonly ISellerWalletReversalService _walletReversal;
    private readonly ICustomerWalletService _customerWallet;
    private readonly IOrderStatusHistoryService _orderStatusHistory;

    public DisputeAdminService(
        ApplicationDbContext context,
        ILogger<DisputeAdminService> logger,
        INotificationService notifications,
        ISellerWalletReversalService walletReversal,
        ICustomerWalletService customerWallet,
        IOrderStatusHistoryService orderStatusHistory)
    {
        _context = context;
        _logger = logger;
        _notifications = notifications;
        _walletReversal = walletReversal;
        _customerWallet = customerWallet;
        _orderStatusHistory = orderStatusHistory;
    }

    public async Task<DisputeListResponseDto> GetAllDisputesAsync(
        int page, 
        int pageSize, 
        short? status = null,
        short? type = null,
        Guid? customerId = null)
    {
        try
        {
            var query = _context.Disputes
                .Include(d => d.Customer)
                .Include(d => d.Shop)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(d => d.Status == status.Value);
            }

            if (type.HasValue)
            {
                query = query.Where(d => d.Type == type.Value);
            }

            if (customerId.HasValue)
            {
                query = query.Where(d => d.CustomerId == customerId.Value);
            }

            var totalCount = await query.CountAsync();

            // Lấy raw data (bao gồm JSON strings) để deserialize in-memory sau ToListAsync
            var rawList = await query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.Id,
                    d.OrderId,
                    OrderTotal = d.Order.Total,
                    d.CustomerId,
                    CustomerName = d.Customer.FullName ?? "N/A",
                    d.ShopId,
                    ShopName = d.Shop.Name,
                    d.Type,
                    d.Status,
                    d.Title,
                    d.Reason,
                    d.RequestedAmount,
                    d.ApprovedAmount,
                    d.SellerResponse,
                    d.SellerRespondedAt,
                    d.Resolution,
                    d.AdminNote,
                    d.ResolvedBy,
                    d.ResolvedAt,
                    d.CreatedAt,
                    d.UpdatedAt,
                    d.EvidenceUrls,
                    d.SellerEvidenceUrls,
                    d.CustomerNote,
                })
                .ToListAsync();

            // Tải DisputeOrderItems của tất cả dispute trong trang bằng một query batch
            var disputeIds = rawList.Select(d => d.Id).ToList();
            var allLineItems = await _context.DisputeOrderItems
                .AsNoTracking()
                .Where(x => disputeIds.Contains(x.DisputeId))
                .Include(x => x.OrderItem)
                .ToListAsync();

            var itemsByDispute = allLineItems
                .GroupBy(x => x.DisputeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var disputes = rawList.Select(d => new DisputeAdminDto
            {
                Id = d.Id,
                OrderId = d.OrderId,
                OrderTotal = d.OrderTotal,
                CustomerId = d.CustomerId,
                CustomerName = d.CustomerName,
                ShopId = d.ShopId,
                ShopName = d.ShopName,
                Type = d.Type,
                TypeName = ((DisputeType)d.Type).ToString(),
                Status = d.Status,
                StatusName = ((DisputeStatus)d.Status).ToString(),
                Title = d.Title,
                Reason = d.Reason,
                RequestedAmount = d.RequestedAmount,
                ApprovedAmount = d.ApprovedAmount,
                SellerResponse = d.SellerResponse,
                SellerRespondedAt = d.SellerRespondedAt,
                Resolution = d.Resolution,
                AdminNote = d.AdminNote,
                ResolvedBy = d.ResolvedBy,
                ResolvedAt = d.ResolvedAt,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
                EvidenceUrls = TryDeserializeUrls(d.EvidenceUrls),
                SellerEvidenceUrls = TryDeserializeUrls(d.SellerEvidenceUrls),
                CustomerNote = d.CustomerNote,
                AffectedItems = itemsByDispute.TryGetValue(d.Id, out var rows)
                    ? rows
                        .Select(r => new DisputeAffectedItemDto
                        {
                            OrderItemId = r.OrderItemId,
                            ProductName = r.OrderItem?.ProductName ?? "",
                            Quantity = r.Quantity,
                            UnitPrice = r.UnitPriceSnapshot,
                            LineTotal = r.LineSnapshotTotal,
                        })
                        .OrderBy(x => x.ProductName)
                        .ToList()
                    : new List<DisputeAffectedItemDto>(),
            }).ToList();

            return new DisputeListResponseDto
            {
                Success = true,
                Disputes = disputes,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting disputes");
            return new DisputeListResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy danh sách khiếu nại"
            };
        }
    }

    public async Task<DisputeResponseDto> GetDisputeByIdAsync(Guid disputeId)
    {
        try
        {
            var raw = await _context.Disputes
                .Include(d => d.Customer)
                .Include(d => d.Shop)
                .Include(d => d.Order)
                .Where(d => d.Id == disputeId)
                .Select(d => new
                {
                    d.Id, d.OrderId,
                    OrderTotal = d.Order.Total,
                    d.CustomerId,
                    CustomerName = d.Customer.FullName ?? "N/A",
                    d.ShopId,
                    ShopName = d.Shop.Name,
                    d.Type, d.Status, d.Title, d.Reason,
                    d.RequestedAmount, d.ApprovedAmount,
                    d.SellerResponse, d.SellerRespondedAt,
                    d.Resolution, d.AdminNote,
                    d.ResolvedBy, d.ResolvedAt,
                    d.CreatedAt, d.UpdatedAt,
                    d.EvidenceUrls,
                    d.SellerEvidenceUrls,
                    d.CustomerNote,
                })
                .FirstOrDefaultAsync();

            if (raw == null)
                return new DisputeResponseDto { Success = false, Message = "Không tìm thấy khiếu nại" };

            var dispute = new DisputeAdminDto
            {
                Id = raw.Id,
                OrderId = raw.OrderId,
                OrderTotal = raw.OrderTotal,
                CustomerId = raw.CustomerId,
                CustomerName = raw.CustomerName,
                ShopId = raw.ShopId,
                ShopName = raw.ShopName,
                Type = raw.Type,
                TypeName = ((DisputeType)raw.Type).ToString(),
                Status = raw.Status,
                StatusName = ((DisputeStatus)raw.Status).ToString(),
                Title = raw.Title,
                Reason = raw.Reason,
                RequestedAmount = raw.RequestedAmount,
                ApprovedAmount = raw.ApprovedAmount,
                SellerResponse = raw.SellerResponse,
                SellerRespondedAt = raw.SellerRespondedAt,
                Resolution = raw.Resolution,
                AdminNote = raw.AdminNote,
                ResolvedBy = raw.ResolvedBy,
                ResolvedAt = raw.ResolvedAt,
                CreatedAt = raw.CreatedAt,
                UpdatedAt = raw.UpdatedAt,
                EvidenceUrls = TryDeserializeUrls(raw.EvidenceUrls),
                SellerEvidenceUrls = TryDeserializeUrls(raw.SellerEvidenceUrls),
                CustomerNote = raw.CustomerNote,
            };

            var lineRows = await _context.DisputeOrderItems
                .AsNoTracking()
                .Where(x => x.DisputeId == disputeId)
                .Include(x => x.OrderItem)
                .ToListAsync();

            dispute.AffectedItems = lineRows
                .Select(r => new DisputeAffectedItemDto
                {
                    OrderItemId = r.OrderItemId,
                    ProductName = r.OrderItem?.ProductName ?? "",
                    Quantity = r.Quantity,
                    UnitPrice = r.UnitPriceSnapshot,
                    LineTotal = r.LineSnapshotTotal
                })
                .OrderBy(x => x.ProductName)
                .ToList();

            return new DisputeResponseDto { Success = true, Dispute = dispute };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dispute: {DisputeId}", disputeId);
            return new DisputeResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy thông tin khiếu nại"
            };
        }
    }

    public async Task<DisputeResponseDto> ApproveRefundAsync(
        Guid disputeId, 
        ApproveRefundDto dto, 
        Guid adminId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            return await ApproveRefundInternalAsync(disputeId, dto, adminId, transaction);
        });
    }

    private async Task<DisputeResponseDto> ApproveRefundInternalAsync(
        Guid disputeId,
        ApproveRefundDto dto,
        Guid adminId,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try
        {
            var dispute = await _context.Disputes
                .Include(d => d.Order)
                .Include(d => d.Shop)
                .Include(d => d.DisputeOrderItems)
                .FirstOrDefaultAsync(d => d.Id == disputeId);

            if (dispute == null)
            {
                return new DisputeResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy khiếu nại"
                };
            }

            if (dispute.Status == (short)DisputeStatus.Resolved || 
                dispute.Status == (short)DisputeStatus.Refunded)
            {
                return new DisputeResponseDto
                {
                    Success = false,
                    Message = "Khiếu nại đã được xử lý"
                };
            }

            var lineSum = dispute.DisputeOrderItems.Sum(x => x.LineSnapshotTotal);

            // Trần hoàn: theo yêu cầu / tổng đơn; nếu có dòng hàng khiếu nại thì không vượt quá giá trị phần hàng đó.
            var refundCeiling = dispute.RequestedAmount > 0
                ? dispute.RequestedAmount
                : dispute.Order.Total;

            if (lineSum > 0)
                refundCeiling = Math.Min(refundCeiling, lineSum);

            decimal approvedAmount;
            if (dto.ApprovedAmount.HasValue)
                approvedAmount = dto.ApprovedAmount.Value;
            else if (dispute.RequestedAmount > 0)
                approvedAmount = dispute.RequestedAmount;
            else
            {
                return new DisputeResponseDto
                {
                    Success = false,
                    Message = "Vui lòng nhập số tiền hoàn (khiếu nại không có số tiền yêu cầu cụ thể)."
                };
            }

            if (approvedAmount < 0)
            {
                return new DisputeResponseDto
                {
                    Success = false,
                    Message = "Số tiền hoàn không được âm"
                };
            }

            if (approvedAmount > refundCeiling)
            {
                return new DisputeResponseDto
                {
                    Success = false,
                    Message = dispute.RequestedAmount > 0
                        ? "Số tiền duyệt không được vượt quá số tiền khách yêu cầu"
                        : $"Số tiền duyệt không được vượt quá tổng đơn hàng ({dispute.Order.Total:N0} VND)."
                };
            }

            if (approvedAmount > dispute.Order.Total)
            {
                return new DisputeResponseDto
                {
                    Success = false,
                    Message = $"Số tiền hoàn không được vượt quá tổng đơn hàng ({dispute.Order.Total:N0} VND)."
                };
            }

            // Update dispute
            dispute.Status = (short)DisputeStatus.Refunded;
            dispute.ApprovedAmount = approvedAmount;
            dispute.Resolution = dto.Resolution;
            dispute.AdminNote = dto.AdminNote;
            dispute.ResolvedBy = adminId;
            dispute.ResolvedAt = DateTime.UtcNow;
            dispute.UpdatedAt = DateTime.UtcNow;

            // Update order status
            var orderPrevStatus = dispute.Order.Status;
            dispute.Order.Status = (short)OrderStatus.Refunded;
            dispute.Order.UpdatedAt = DateTime.UtcNow;
            _orderStatusHistory.AddEntry(
                dispute.OrderId,
                orderPrevStatus,
                (short)OrderStatus.Refunded,
                adminId,
                "Hoàn tiền khiếu nại (admin duyệt)");

            await _walletReversal.TryReverseSettlementForOrderAsync(dispute.OrderId, "Hoàn tiền khiếu nại");

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (approvedAmount > 0)
            {
                await _customerWallet.CreditRefundAsync(
                    dispute.CustomerId,
                    approvedAmount,
                    "Order",
                    dispute.OrderId,
                    $"Hoàn tiền khiếu nại đơn #{NotificationFormatting.ShortEntityId(dispute.OrderId)}");
            }

            _logger.LogInformation(
                "Dispute approved and refunded: {DisputeId} by admin: {AdminId}. Amount: {Amount}", 
                disputeId, adminId, dispute.ApprovedAmount);

            var orderRef = NotificationFormatting.ShortEntityId(dispute.OrderId);
            await _notifications.PublishAsync(
                dispute.CustomerId,
                nameof(NotificationType.Dispute),
                "Hoàn tiền khiếu nại",
                $"Khiếu nại cho đơn #{orderRef} đã được chấp nhận hoàn tiền. Số tiền: {dispute.ApprovedAmount:N0} VND.",
                "Dispute",
                dispute.Id,
                queueEmail: true);

            await _notifications.PublishAsync(
                dispute.Shop.OwnerId,
                nameof(NotificationType.Dispute),
                "Khiếu nại — hoàn tiền",
                $"Admin đã phê duyệt hoàn tiền cho khiếu nại đơn #{orderRef}.",
                "Dispute",
                dispute.Id,
                queueEmail: true);

            return new DisputeResponseDto
            {
                Success = true,
                Message = "Đã duyệt hoàn tiền thành công"
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error approving refund for dispute: {DisputeId}", disputeId);
            return new DisputeResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi duyệt hoàn tiền"
            };
        }
    }

    public async Task<DisputeResponseDto> RejectDisputeAsync(
        Guid disputeId, 
        RejectDisputeDto dto, 
        Guid adminId)
    {
        try
        {
            var dispute = await _context.Disputes
                .Include(d => d.Shop)
                .FirstOrDefaultAsync(d => d.Id == disputeId);

            if (dispute == null)
            {
                return new DisputeResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy khiếu nại"
                };
            }

            if (dispute.Status == (short)DisputeStatus.Resolved || 
                dispute.Status == (short)DisputeStatus.Rejected)
            {
                return new DisputeResponseDto
                {
                    Success = false,
                    Message = "Khiếu nại đã được xử lý"
                };
            }

            dispute.Status = (short)DisputeStatus.Rejected;
            dispute.Resolution = dto.Resolution;
            dispute.AdminNote = dto.AdminNote;
            dispute.ResolvedBy = adminId;
            dispute.ResolvedAt = DateTime.UtcNow;
            dispute.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Dispute rejected: {DisputeId} by admin: {AdminId}", 
                disputeId, adminId);

            var orderRef = NotificationFormatting.ShortEntityId(dispute.OrderId);
            var resolution = string.IsNullOrWhiteSpace(dto.Resolution) ? "" : $" Lý do: {dto.Resolution}";
            await _notifications.PublishAsync(
                dispute.CustomerId,
                nameof(NotificationType.Dispute),
                "Khiếu nại bị từ chối",
                $"Khiếu nại cho đơn #{orderRef} đã bị từ chối.{resolution}",
                "Dispute",
                dispute.Id,
                queueEmail: true);

            await _notifications.PublishAsync(
                dispute.Shop.OwnerId,
                nameof(NotificationType.Dispute),
                "Khiếu nại — quyết định admin",
                $"Admin đã từ chối khiếu nại cho đơn #{orderRef}.{resolution}",
                "Dispute",
                dispute.Id,
                queueEmail: true);

            return new DisputeResponseDto
            {
                Success = true,
                Message = "Đã từ chối khiếu nại"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting dispute: {DisputeId}", disputeId);
            return new DisputeResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi từ chối khiếu nại"
            };
        }
    }

    public async Task<DisputeResponseDto> RequestSellerResponseAsync(
        Guid disputeId, RequestResponseDto dto, Guid adminId)
    {
        try
        {
            var dispute = await _context.Disputes
                .Include(d => d.Shop)
                .FirstOrDefaultAsync(d => d.Id == disputeId);

            if (dispute == null)
                return new DisputeResponseDto { Success = false, Message = "Không tìm thấy khiếu nại" };

            if (FinalStatuses.Contains((DisputeStatus)dispute.Status))
                return new DisputeResponseDto { Success = false, Message = "Khiếu nại đã kết thúc" };

            dispute.Status = (short)DisputeStatus.WaitingSeller;
            if (dto.AdminNote != null) dispute.AdminNote = dto.AdminNote;
            dispute.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var orderRef = NotificationFormatting.ShortEntityId(dispute.OrderId);
            var noteText = string.IsNullOrWhiteSpace(dto.AdminNote) ? "" : $" Yêu cầu: {dto.AdminNote}";
            await _notifications.PublishAsync(
                dispute.Shop.OwnerId,
                nameof(NotificationType.Dispute),
                "Yêu cầu phản hồi khiếu nại",
                $"Admin yêu cầu bạn phản hồi khiếu nại liên quan đơn #{orderRef}.{noteText}",
                "Dispute", dispute.Id, queueEmail: true);

            return new DisputeResponseDto { Success = true, Message = "Đã yêu cầu seller phản hồi" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting seller response: {DisputeId}", disputeId);
            return new DisputeResponseDto { Success = false, Message = "Có lỗi xảy ra" };
        }
    }

    public async Task<DisputeResponseDto> RequestCustomerResponseAsync(
        Guid disputeId, RequestResponseDto dto, Guid adminId)
    {
        try
        {
            var dispute = await _context.Disputes
                .FirstOrDefaultAsync(d => d.Id == disputeId);

            if (dispute == null)
                return new DisputeResponseDto { Success = false, Message = "Không tìm thấy khiếu nại" };

            if (FinalStatuses.Contains((DisputeStatus)dispute.Status))
                return new DisputeResponseDto { Success = false, Message = "Khiếu nại đã kết thúc" };

            dispute.Status = (short)DisputeStatus.WaitingCustomer;
            if (dto.AdminNote != null) dispute.AdminNote = dto.AdminNote;
            dispute.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var orderRef = NotificationFormatting.ShortEntityId(dispute.OrderId);
            var noteText = string.IsNullOrWhiteSpace(dto.AdminNote) ? "" : $" Yêu cầu: {dto.AdminNote}";
            await _notifications.PublishAsync(
                dispute.CustomerId,
                nameof(NotificationType.Dispute),
                "Cần bổ sung thông tin khiếu nại",
                $"Admin yêu cầu bạn bổ sung thông tin khiếu nại đơn #{orderRef}.{noteText}",
                "Dispute", dispute.Id, queueEmail: true);

            return new DisputeResponseDto { Success = true, Message = "Đã yêu cầu customer bổ sung" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting customer response: {DisputeId}", disputeId);
            return new DisputeResponseDto { Success = false, Message = "Có lỗi xảy ra" };
        }
    }

    private static List<string> TryDeserializeUrls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }
}
