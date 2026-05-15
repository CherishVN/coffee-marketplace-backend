using System.Text.Json;
using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Disputes;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Hubs;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class SellerDisputeService : ISellerDisputeService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IOrderStatusHistoryService _orderStatusHistory;
    private readonly IHubContext<OrderTrackingHub> _hubContext;
    private readonly ISellerWalletReversalService _walletReversal;
    private readonly ICustomerWalletService _customerWallet;

    private static readonly DisputeStatus[] FinalStatuses =
    [
        DisputeStatus.Resolved,
        DisputeStatus.Rejected,
        DisputeStatus.Refunded,
        DisputeStatus.Cancelled
    ];

    public SellerDisputeService(
        ApplicationDbContext context,
        INotificationService notifications,
        IOrderStatusHistoryService orderStatusHistory,
        IHubContext<OrderTrackingHub> hubContext,
        ISellerWalletReversalService walletReversal,
        ICustomerWalletService customerWallet)
    {
        _context = context;
        _notifications = notifications;
        _orderStatusHistory = orderStatusHistory;
        _hubContext = hubContext;
        _walletReversal = walletReversal;
        _customerWallet = customerWallet;
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
            .Include(d => d.Order)
                .ThenInclude(o => o.Shipments)
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
            .Include(d => d.Order)
                .ThenInclude(o => o.Shipments)
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

        // Sau khi seller phản hồi -> trả về UnderReview để admin xem xét
        if (dispute.Status == (short)DisputeStatus.WaitingSeller ||
            dispute.Status == (short)DisputeStatus.Pending ||
            dispute.Status == (short)DisputeStatus.UnderReview ||
            dispute.Status == (short)DisputeStatus.WaitingCustomer)
        {
            dispute.Status = (short)DisputeStatus.UnderReview;
        }

        await _context.SaveChangesAsync();

        var orderRef = NotificationFormatting.ShortEntityId(dispute.OrderId);
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

        await NotifyDisputeUpdatedAsync(dispute, "seller");

        return new SellerDisputeResponseDto
        {
            Success = true,
            Message = "Phản hồi thành công",
            Dispute = MapToDto(dispute, dispute.Customer.FullName ?? dispute.Customer.Phone ?? "Khách hàng")
        };
    }

    public async Task<SellerDisputeResponseDto> ApproveReturnAsync(Guid sellerId, Guid disputeId)
    {
        var shop = await _context.Shops.FirstOrDefaultAsync(s => s.OwnerId == sellerId);
        if (shop == null) return Fail("Bạn chưa có shop");

        var dispute = await _context.Disputes
            .Include(d => d.Order)
            .Include(d => d.Customer)
            .FirstOrDefaultAsync(d => d.Id == disputeId && d.ShopId == shop.Id);

        if (dispute == null) return Fail("Không tìm thấy khiếu nại");
        if (FinalStatuses.Contains((DisputeStatus)dispute.Status)) return Fail("Khiếu nại đã kết thúc");
        if (dispute.Type != (short)DisputeType.Return) return Fail("Đây không phải là yêu cầu trả hàng");

        var currentStatus = (OrderStatus)dispute.Order.Status;
        if (currentStatus is not (OrderStatus.Delivered or OrderStatus.Completed))
            return Fail("Chỉ có thể chấp nhận trả hàng khi đơn đã giao hoặc hoàn thành");

        var oldStatus = currentStatus;
        dispute.Order.Status = (short)OrderStatus.Returning;
        dispute.Order.UpdatedAt = DateTime.UtcNow;

        _orderStatusHistory.AddEntry(
            dispute.OrderId,
            (short)oldStatus,
            (short)OrderStatus.Returning,
            sellerId,
            "Seller chấp nhận yêu cầu trả hàng");

        dispute.Status = (short)DisputeStatus.WaitingCustomer;
        dispute.UpdatedAt = DateTime.UtcNow;

        // Tự động tạo vận đơn trả hàng (giả lập GHN) để khách có mã ngay, không cần nhập tay
        var returnTrackingCode = $"RTN-{dispute.Order.OrderCode ?? disputeId.ToString("N")[..8].ToUpper()}";
        var existingReturnShipment = await _context.Shipments
            .FirstOrDefaultAsync(s => s.OrderId == dispute.OrderId && s.ShippingProvider == "Return");
        if (existingReturnShipment == null)
        {
            _context.Shipments.Add(new Shipment
            {
                Id = Guid.NewGuid(),
                OrderId = dispute.OrderId,
                ShopId = shop.Id,
                ShippingProvider = "Return",
                TrackingCode = returnTrackingCode,
                Status = "waiting_customer",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        await _notifications.PublishAsync(
            dispute.CustomerId,
            nameof(NotificationType.Dispute),
            "Yêu cầu trả hàng đã được chấp nhận",
            $"Shop đã chấp nhận trả hàng cho đơn #{NotificationFormatting.ShortEntityId(dispute.OrderId)}. Mã vận đơn trả hàng của bạn là: {returnTrackingCode}. Vui lòng đóng gói hàng và liên hệ GHN để gửi trả.",
            "Dispute", dispute.Id);

        await NotifyOrderStatusChangedAsync(dispute.Order, oldStatus, OrderStatus.Returning, "seller");
        await NotifyDisputeUpdatedAsync(dispute, "seller");

        return new SellerDisputeResponseDto { Success = true, Message = "Đã chấp nhận trả hàng" };
    }

    public async Task<SellerDisputeResponseDto> ConfirmReturnReceiptAsync(Guid sellerId, Guid disputeId, ConfirmReturnReceiptDto dto)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var shop = await _context.Shops.FirstOrDefaultAsync(s => s.OwnerId == sellerId);
                if (shop == null) return Fail("Bạn chưa có shop");

                var dispute = await _context.Disputes
                    .Include(d => d.Order)
                    .Include(d => d.Customer)
                    .Include(d => d.DisputeOrderItems)
                    .FirstOrDefaultAsync(d => d.Id == disputeId && d.ShopId == shop.Id);

                if (dispute == null) return Fail("Không tìm thấy khiếu nại");
                if (FinalStatuses.Contains((DisputeStatus)dispute.Status)) return Fail("Khiếu nại đã kết thúc");
                if (dispute.Type != (short)DisputeType.Return) return Fail("Đây không phải là yêu cầu trả hàng");
                
                // Chuẩn business: Phải đợi GHN giao về (Returned) mới cho phép Seller xác nhận nhận hàng
                if (dispute.Order.Status != (short)OrderStatus.Returned) 
                    return Fail("Vận đơn trả hàng chưa được GHN xác nhận giao về kho. Vui lòng đợi hàng về.");

                var oldStatus = (OrderStatus)dispute.Order.Status;

                // Lưu ảnh bằng chứng vào vận đơn trả hàng
                var returnShipment = await _context.Shipments
                    .FirstOrDefaultAsync(s => s.OrderId == dispute.OrderId && s.ShippingProvider == "Return");
                if (returnShipment != null && dto.EvidenceUrls != null && dto.EvidenceUrls.Count > 0)
                {
                    returnShipment.DeliveryProofUrls = System.Text.Json.JsonSerializer.Serialize(dto.EvidenceUrls);
                    _context.Shipments.Update(returnShipment);
                }
                
                var lineSum = dispute.DisputeOrderItems.Sum(x => x.LineSnapshotTotal);
                var refundCeiling = dispute.RequestedAmount > 0 ? dispute.RequestedAmount : dispute.Order.Total;
                if (lineSum > 0) refundCeiling = Math.Min(refundCeiling, lineSum);
                if (refundCeiling <= 0) refundCeiling = dispute.Order.Total;
                var approvedAmount = refundCeiling;

                dispute.Order.Status = (short)OrderStatus.Refunded;
                dispute.Order.UpdatedAt = DateTime.UtcNow;

                _orderStatusHistory.AddEntry(
                    dispute.OrderId,
                    (short)oldStatus,
                    (short)OrderStatus.Refunded,
                    sellerId,
                    "Seller xác nhận đã nhận hàng trả về (tự động hoàn tiền)");

                dispute.Status = (short)DisputeStatus.Refunded;
                dispute.ApprovedAmount = approvedAmount;
                dispute.Resolution = "Tự động hoàn tiền do Seller xác nhận đã nhận hàng";
                dispute.UpdatedAt = DateTime.UtcNow;
                dispute.ResolvedAt = DateTime.UtcNow;

                await _walletReversal.TryReverseSettlementForOrderAsync(dispute.OrderId, "Hoàn tiền khiếu nại (return)");
                
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

                await _notifications.PublishAsync(
                    dispute.CustomerId,
                    nameof(NotificationType.Dispute),
                    "Đã hoàn tiền trả hàng",
                    $"Shop đã nhận được hàng trả cho đơn #{NotificationFormatting.ShortEntityId(dispute.OrderId)}. Số tiền {approvedAmount:N0} VND đã được hoàn vào ví của bạn.",
                    "Dispute", dispute.Id, queueEmail: true);

                await NotifyOrderStatusChangedAsync(dispute.Order, oldStatus, OrderStatus.Refunded, "seller");
                await NotifyDisputeUpdatedAsync(dispute, "seller");

                return new SellerDisputeResponseDto { Success = true, Message = "Đã xác nhận nhận hàng và tự động hoàn tiền" };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return Fail("Có lỗi xảy ra khi xác nhận nhận hàng và hoàn tiền");
            }
        });
    }

    public async Task<SellerDisputeResponseDto> ApproveRefundAsync(Guid sellerId, Guid disputeId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var shop = await _context.Shops.FirstOrDefaultAsync(s => s.OwnerId == sellerId);
                if (shop == null) return Fail("Bạn chưa có shop");

                var dispute = await _context.Disputes
                    .Include(d => d.Order)
                    .Include(d => d.Customer)
                    .Include(d => d.DisputeOrderItems)
                    .FirstOrDefaultAsync(d => d.Id == disputeId && d.ShopId == shop.Id);

                if (dispute == null) return Fail("Không tìm thấy khiếu nại");
                if (FinalStatuses.Contains((DisputeStatus)dispute.Status)) return Fail("Khiếu nại đã kết thúc");
                if (dispute.Type == (short)DisputeType.Return) return Fail("Đây là yêu cầu trả hàng, hãy dùng chức năng xác nhận trả hàng");

                var oldStatus = (OrderStatus)dispute.Order.Status;
                
                var lineSum = dispute.DisputeOrderItems.Sum(x => x.LineSnapshotTotal);
                var refundCeiling = dispute.RequestedAmount > 0 ? dispute.RequestedAmount : dispute.Order.Total;
                if (lineSum > 0) refundCeiling = Math.Min(refundCeiling, lineSum);
                if (refundCeiling <= 0) refundCeiling = dispute.Order.Total;
                var approvedAmount = refundCeiling;

                dispute.Order.Status = (short)OrderStatus.Refunded;
                dispute.Order.UpdatedAt = DateTime.UtcNow;

                _orderStatusHistory.AddEntry(
                    dispute.OrderId,
                    (short)oldStatus,
                    (short)OrderStatus.Refunded,
                    sellerId,
                    "Seller chấp nhận yêu cầu hoàn tiền");

                dispute.Status = (short)DisputeStatus.Refunded;
                dispute.ApprovedAmount = approvedAmount;
                dispute.Resolution = "Seller chấp nhận yêu cầu hoàn tiền";
                dispute.UpdatedAt = DateTime.UtcNow;
                dispute.ResolvedAt = DateTime.UtcNow;

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

                await _notifications.PublishAsync(
                    dispute.CustomerId,
                    nameof(NotificationType.Dispute),
                    "Yêu cầu hoàn tiền được chấp nhận",
                    $"Shop đã chấp nhận yêu cầu hoàn tiền cho đơn #{NotificationFormatting.ShortEntityId(dispute.OrderId)}. Số tiền {approvedAmount:N0} VND đã được hoàn vào ví của bạn.",
                    "Dispute", dispute.Id, queueEmail: true);

                await NotifyOrderStatusChangedAsync(dispute.Order, oldStatus, OrderStatus.Refunded, "seller");
                await NotifyDisputeUpdatedAsync(dispute, "seller");

                return new SellerDisputeResponseDto { Success = true, Message = "Đã chấp nhận hoàn tiền thành công" };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return Fail("Có lỗi xảy ra khi hoàn tiền");
            }
        });
    }

    private Task NotifyOrderStatusChangedAsync(Order order, OrderStatus oldStatus, OrderStatus newStatus, string source)
    {
        var customerGroup = OrderTrackingHub.GetUserGroupName(order.CustomerId);
        var sellerGroup = OrderTrackingHub.GetUserGroupName(order.Shop.OwnerId);

        var data = new
        {
            orderId = order.Id,
            oldStatus = (short)oldStatus,
            oldStatusName = OrderStatusVnHelper.Vietnamese(oldStatus),
            newStatus = (short)newStatus,
            newStatusName = OrderStatusVnHelper.Vietnamese(newStatus),
            updatedAt = order.UpdatedAt,
            source
        };

        return _hubContext.Clients.Groups(customerGroup, sellerGroup).SendAsync(
            "OrderStatusUpdated",
            data,
            cancellationToken: CancellationToken.None);
    }

    private Task NotifyDisputeUpdatedAsync(Dispute dispute, string source)
    {
        var customerGroup = OrderTrackingHub.GetUserGroupName(dispute.CustomerId);
        var sellerGroup = OrderTrackingHub.GetUserGroupName(dispute.Shop.OwnerId);

        var data = new
        {
            disputeId = dispute.Id,
            orderId = dispute.OrderId,
            status = dispute.Status,
            statusName = ((DisputeStatus)dispute.Status).ToString(),
            type = dispute.Type,
            typeName = ((DisputeType)dispute.Type).ToString(),
            updatedAt = dispute.UpdatedAt,
            source
        };

        return _hubContext.Clients.Groups(customerGroup, sellerGroup)
            .SendAsync("DisputeUpdated", data, cancellationToken: CancellationToken.None);
    }

    private static SellerDisputeDto MapToDto(Dispute dispute, string customerName)
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
            AffectedItems = MapSellerAffectedItems(dispute),
            OrderStatus = dispute.Order?.Status,
            ReturnTrackingCode = dispute.Order?.Shipments?
                .FirstOrDefault(s => s.TrackingCode != null && s.TrackingCode.StartsWith("RTN-", StringComparison.OrdinalIgnoreCase))?
                .TrackingCode,
            ReturnShipmentEvidenceUrls = TryDeserializeUrls(dispute.Order?.Shipments?
                .FirstOrDefault(s => s.TrackingCode != null && s.TrackingCode.StartsWith("RTN-", StringComparison.OrdinalIgnoreCase))?
                .DeliveryProofUrls)
        };
    }

    private static List<DisputeAffectedItemDto> MapSellerAffectedItems(Dispute dispute)
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
    public async Task<SellerDisputeResponseDto> RejectDisputeAsync(Guid sellerId, Guid disputeId, SellerRespondDisputeDto dto)
    {
        var shop = await _context.Shops.FirstOrDefaultAsync(s => s.OwnerId == sellerId);
        if (shop == null) return Fail("Bạn chưa có shop");

        var dispute = await _context.Disputes
            .Include(d => d.Shop)
            .Include(d => d.Order)
            .FirstOrDefaultAsync(d => d.Id == disputeId && d.ShopId == shop.Id);

        if (dispute == null) return Fail("Không tìm thấy khiếu nại");
        if (FinalStatuses.Contains((DisputeStatus)dispute.Status)) return Fail("Khiếu nại đã kết thúc");

        // Cập nhật phản hồi của Seller
        dispute.SellerResponse = dto.Response;
        dispute.SellerRespondedAt = DateTime.UtcNow;
        if (dto.EvidenceUrls != null && dto.EvidenceUrls.Count > 0)
        {
            dispute.SellerEvidenceUrls = System.Text.Json.JsonSerializer.Serialize(dto.EvidenceUrls);
        }

        // Khi Seller từ chối, chuyển sang trạng thái Đang xem xét (Admin phân xử)
        dispute.Status = (short)DisputeStatus.UnderReview;
        dispute.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var ordRef = NotificationFormatting.ShortEntityId(dispute.OrderId);
        
        // Thông báo cho khách hàng
        await _notifications.PublishAsync(
            dispute.CustomerId,
            nameof(NotificationType.Dispute),
            "Khiếu nại bị từ chối",
            $"Shop đã từ chối yêu cầu cho đơn #{ordRef}. Admin sàn sẽ tiến hành phân xử.",
            "Dispute", dispute.Id, queueEmail: true);

        // Thông báo cho Admin
        await _notifications.PublishToUsersWithRoleAsync(
            "admin",
            nameof(NotificationType.Dispute),
            "Cần phân xử khiếu nại",
            $"Đơn #{ordRef}: Shop đã từ chối khiếu nại và yêu cầu sàn can thiệp.",
            "Dispute", dispute.Id, queueEmail: false);

        await NotifyDisputeUpdatedAsync(dispute, "seller");

        return new SellerDisputeResponseDto { Success = true, Message = "Đã từ chối khiếu nại và chuyển cho Admin phân xử" };
    }
}
