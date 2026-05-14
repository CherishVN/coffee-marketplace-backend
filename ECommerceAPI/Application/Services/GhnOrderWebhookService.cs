using System.Text.Json;
using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Webhooks;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Hubs;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class GhnOrderWebhookService : IGhnOrderWebhookService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<OrderTrackingHub> _hubContext;
    private readonly INotificationService _notifications;
    private readonly ISellerWalletReversalService _walletReversal;
    private readonly IOrderNotificationEmailComposer _orderEmailComposer;
    private readonly IOrderStatusHistoryService _orderStatusHistory;
    private readonly ILogger<GhnOrderWebhookService> _logger;

    public GhnOrderWebhookService(
        ApplicationDbContext context,
        IHubContext<OrderTrackingHub> hubContext,
        INotificationService notifications,
        ISellerWalletReversalService walletReversal,
        IOrderNotificationEmailComposer orderEmailComposer,
        IOrderStatusHistoryService orderStatusHistory,
        ILogger<GhnOrderWebhookService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _notifications = notifications;
        _walletReversal = walletReversal;
        _orderEmailComposer = orderEmailComposer;
        _orderStatusHistory = orderStatusHistory;
        _logger = logger;
    }

    public async Task<GhnOrderWebhookResult> ProcessOrderStatusAsync(
        GhnOrderStatusPayload payload,
        bool validateShopId,
        CancellationToken cancellationToken = default)
    {
        var client = payload.ClientOrderCode?.Trim();
        var ghnCode = payload.OrderCode?.Trim();

        if (string.IsNullOrEmpty(client) && string.IsNullOrEmpty(ghnCode))
        {
            return new GhnOrderWebhookResult
            {
                Handled = true,
                Message = "Thiếu OrderCode và ClientOrderCode"
            };
        }

        var order = await FindOrderByGhnReferenceAsync(client, ghnCode, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning(
                "GHN webhook: không tìm thấy đơn (ClientOrderCode={Client}, OrderCode={Ghn})",
                client, ghnCode);
            return new GhnOrderWebhookResult
            {
                Handled = true,
                Message = "Không tìm thấy đơn tương ứng"
            };
        }

        if (validateShopId
            && payload.ShopID is > 0
            && order.Shop?.GhnShopId is > 0
            && order.Shop.GhnShopId != payload.ShopID)
        {
            _logger.LogWarning(
                "GHN webhook: ShopID không khớp. Order {OrderId}, expected {Exp}, got {Got}",
                order.Id, order.Shop.GhnShopId, payload.ShopID);
            return new GhnOrderWebhookResult
            {
                Handled = true,
                Message = "ShopID không khớp"
            };
        }

        order.UpdatedAt = DateTime.UtcNow;

        await UpsertShipmentFromGhnPayloadAsync(order, payload, ghnCode, cancellationToken);

        var newMapped = MapGhnStatusToOrderStatus(payload.Status, payload.Type);
        var oldStatus = (OrderStatus)order.Status;

        if (newMapped == null)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("GHN webhook: trạng thái GHN chưa map ({Status}) — chỉ cập nhật phí/tracking. Order {OrderId}", payload.Status, order.Id);
            return new GhnOrderWebhookResult
            {
                Handled = true,
                Message = "Đã cập nhật phí/tracking, không đổi trạng thái (status GHN chưa map)",
                OrderId = order.Id
            };
        }

        var next = newMapped!.Value;

        if (oldStatus is OrderStatus.Completed or OrderStatus.Refunded)
        {
            await _context.SaveChangesAsync(cancellationToken);
            return new GhnOrderWebhookResult
            {
                Handled = true,
                Message = "Đơn đã hoàn tất / hoàn tiền — không đổi trạng thái từ GHN",
                OrderId = order.Id
            };
        }

        if (oldStatus == OrderStatus.Cancelled && next != OrderStatus.Cancelled)
        {
            await _context.SaveChangesAsync(cancellationToken);
            return new GhnOrderWebhookResult
            {
                Handled = true,
                Message = "Đơn đã hủy — không khôi phục từ GHN",
                OrderId = order.Id
            };
        }

        if (oldStatus == OrderStatus.PendingPayment)
        {
            _logger.LogInformation("GHN webhook: bỏ qua cập nhật trạng thái — đơn {OrderId} còn chờ thanh toán", order.Id);
            await _context.SaveChangesAsync(cancellationToken);
            return new GhnOrderWebhookResult
            {
                Handled = true,
                Message = "Chờ thanh toán — không cập nhật trạng thái vận chuyển",
                OrderId = order.Id
            };
        }

        if (oldStatus == OrderStatus.PendingConfirmation)
        {
            _logger.LogInformation("GHN webhook: bỏ qua cập nhật trạng thái — đơn {OrderId} chờ shop xác nhận", order.Id);
            await _context.SaveChangesAsync(cancellationToken);
            return new GhnOrderWebhookResult
            {
                Handled = true,
                Message = "Chờ shop xác nhận — không cập nhật trạng thái vận chuyển từ GHN",
                OrderId = order.Id
            };
        }

        if (oldStatus == next)
        {
            await _context.SaveChangesAsync(cancellationToken);
            return new GhnOrderWebhookResult
            {
                Handled = true,
                Message = "Trạng thái không đổi",
                OrderId = order.Id
            };
        }

        if (!IsAllowedTransition(oldStatus, next))
        {
            _logger.LogInformation(
                "GHN webhook: bỏ qua chuyển {Old} → {New} (không hợp lệ). Order {OrderId}",
                oldStatus, next, order.Id);
            await _context.SaveChangesAsync(cancellationToken);
            return new GhnOrderWebhookResult
            {
                Handled = true,
                Message = "Chuyển trạng thái bị từ chối theo quy tắc nội bộ",
                OrderId = order.Id
            };
        }

        order.Status = (short)next;
        _orderStatusHistory.AddEntry(
            order.Id,
            (short)oldStatus,
            (short)next,
            null,
            $"Webhook GHN: {payload.Status ?? "?"} → {next}"
        );

        if (next == OrderStatus.Delivered && oldStatus != OrderStatus.Delivered)
        {
            var primary = order.PrimaryShipment();
            if (primary != null && !primary.ActualDeliveryDate.HasValue)
            {
                var t = payload.Time;
                if (t.HasValue)
                {
                    var dt = t.Value;
                    if (dt.Kind == DateTimeKind.Unspecified)
                        dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    primary.ActualDeliveryDate = new DateTimeOffset(dt, TimeSpan.Zero);
                }
                else
                {
                    primary.ActualDeliveryDate = DateTimeOffset.UtcNow;
                }

                primary.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (next == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(order.CancelReason))
            order.CancelReason = string.IsNullOrWhiteSpace(payload.Description)
                ? "Hủy từ đối tác vận chuyển (GHN)"
                : payload.Description!.Trim()[..Math.Min(500, payload.Description.Trim().Length)];

        if (next is OrderStatus.Cancelled or OrderStatus.Refunded)
        {
            await _walletReversal.TryReverseSettlementForOrderAsync(
                order.Id,
                $"Cập nhật từ GHN webhook → {next}");
        }

        await _context.SaveChangesAsync(cancellationToken);

        await NotifyStatusChangedAsync(order, oldStatus, next);

        var code = NotificationFormatting.ShortEntityId(order.Id);
        var composed = await _orderEmailComposer.TryComposeAsync(order.Id, oldStatus, next);
        await _notifications.PublishAsync(
            order.CustomerId,
            nameof(NotificationType.Order),
            "Cập nhật trạng thái đơn hàng",
            $"Đơn #{code} chuyển từ {OrderStatusVnHelper.Vietnamese(oldStatus)} sang {OrderStatusVnHelper.Vietnamese(next)}.",
            "Order",
            order.Id,
            queueEmail: true,
            emailHtmlBody: composed?.Html,
            emailSubjectOverride: composed?.Subject);

        _logger.LogInformation(
            "GHN webhook: order {OrderId} {Old} → {New} (GHN status {GhnStatus})",
            order.Id, oldStatus, next, payload.Status);

        return new GhnOrderWebhookResult
        {
            Handled = true,
            Message = "OK",
            OrderId = order.Id
        };
    }

    private async Task<Order?> FindOrderByGhnReferenceAsync(
        string? clientOrder,
        string? ghnOrderCode,
        CancellationToken cancellationToken)
    {
        IQueryable<Order> q = _context.Orders
            .Include(o => o.Shipments)
            .Include(o => o.Shop)
            .Include(o => o.OrderItems);

        if (!string.IsNullOrEmpty(clientOrder))
        {
            var o = await q.FirstOrDefaultAsync(x => x.OrderCode == clientOrder, cancellationToken);
            if (o != null) return o;
        }

        if (!string.IsNullOrEmpty(ghnOrderCode))
        {
            var ghn = ghnOrderCode.Trim();
            var orderIdFromShipment = await _context.Shipments
                .AsNoTracking()
                .Where(s => s.TrackingCode.ToLower() == ghn.ToLower())
                .Select(s => s.OrderId)
                .FirstOrDefaultAsync(cancellationToken);
            if (orderIdFromShipment != default)
            {
                var o = await q.FirstOrDefaultAsync(x => x.Id == orderIdFromShipment, cancellationToken);
                if (o != null) return o;
            }

            var o2 = await q.FirstOrDefaultAsync(x => x.OrderCode == ghn, cancellationToken);
            if (o2 != null) return o2;
        }

        return null;
    }

    private async Task UpsertShipmentFromGhnPayloadAsync(
        Order order,
        GhnOrderStatusPayload payload,
        string? ghnOrderCode,
        CancellationToken cancellationToken)
    {
        var st = (OrderStatus)order.Status;
        if (st is OrderStatus.PendingPayment or OrderStatus.PendingConfirmation)
            return;

        var primary = order.PrimaryShipment();
        var ghn = ghnOrderCode?.Trim();
        var tracking = !string.IsNullOrEmpty(ghn) ? ghn! : primary?.TrackingCode?.Trim();
        if (string.IsNullOrEmpty(tracking)) return;

        var raw = string.IsNullOrWhiteSpace(payload.Status) ? "unknown" : payload.Status.Trim();

        var row = await _context.Shipments
            .FirstOrDefaultAsync(
                s => s.TrackingCode.ToLower() == tracking.ToLower(),
                cancellationToken);

        if (row == null
            && !string.IsNullOrEmpty(ghn)
            && primary != null
            && !string.IsNullOrEmpty(primary.TrackingCode)
            && primary.TrackingCode.StartsWith("PEND-", StringComparison.OrdinalIgnoreCase)
            && !await _context.Shipments.AnyAsync(
                s => s.TrackingCode.ToLower() == ghn!.ToLower() && s.Id != primary.Id,
                cancellationToken))
        {
            primary.TrackingCode = ghn;
            primary.UpdatedAt = DateTime.UtcNow;
            row = primary;
        }

        if (row == null)
        {
            var created = new Shipment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ShopId = order.ShopId,
                ShippingProvider = "GHN",
                ShippingServiceId = primary?.ShippingServiceId,
                TrackingCode = tracking,
                Status = raw,
                ProviderShippingFee = payload.TotalFee is > 0
                    ? payload.TotalFee.Value
                    : order.ShippingFee,
                CodAmount = payload.CODAmount ?? 0,
                EstimatedDeliveryDate = primary?.EstimatedDeliveryDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            if (string.Equals(raw, "delivered", StringComparison.OrdinalIgnoreCase))
                created.ActualDeliveryDate = ParseGhnEventTimeToOffset(payload.Time) ?? DateTimeOffset.UtcNow;
            if (payload.DeliveryProofUrls is { Count: > 0 })
            {
                var existing = string.IsNullOrEmpty(created.DeliveryProofUrls)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(created.DeliveryProofUrls) ?? new List<string>();
                var merged = existing.Union(payload.DeliveryProofUrls).ToList();
                created.DeliveryProofUrls = JsonSerializer.Serialize(merged);
            }
            _context.Shipments.Add(created);
            return;
        }

        if (row.OrderId != order.Id)
        {
            _logger.LogWarning(
                "GHN webhook: tracking {Tracking} thuộc order {Other}, webhook đang gán cho {Order} — bỏ ghi shipment",
                tracking, row.OrderId, order.Id);
            return;
        }

        row.Status = raw;
        if (payload.TotalFee is > 0) row.ProviderShippingFee = payload.TotalFee.Value;
        if (payload.CODAmount is not null) row.CodAmount = payload.CODAmount.Value;
        if (primary?.ShippingServiceId is { } ssid) row.ShippingServiceId = ssid;
        if (primary?.EstimatedDeliveryDate is { } ed) row.EstimatedDeliveryDate = ed;
        row.UpdatedAt = DateTime.UtcNow;
        if (string.Equals(raw, "delivered", StringComparison.OrdinalIgnoreCase))
            row.ActualDeliveryDate = ParseGhnEventTimeToOffset(payload.Time) ?? DateTimeOffset.UtcNow;
        if (payload.DeliveryProofUrls is { Count: > 0 })
        {
            var existing = string.IsNullOrEmpty(row.DeliveryProofUrls)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(row.DeliveryProofUrls) ?? new List<string>();
            var merged = existing.Union(payload.DeliveryProofUrls).ToList();
            row.DeliveryProofUrls = JsonSerializer.Serialize(merged);
        }
    }

    private static DateTimeOffset? ParseGhnEventTimeToOffset(DateTime? time)
    {
        if (!time.HasValue) return null;
        var dt = time.Value;
        if (dt.Kind == DateTimeKind.Unspecified)
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return new DateTimeOffset(dt, TimeSpan.Zero);
    }

    /// <summary>
    /// Bảng chuyển trạng thái hợp lệ từ GHN webhook.
    /// Mỗi trạng thái chỉ được phép chuyển sang các trạng thái được liệt kê.
    /// </summary>
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> _allowedTransitions = new()
    {
        [OrderStatus.PendingPayment]     = new() { },                                                            // GHN không được đụng vào
        [OrderStatus.PendingConfirmation] = new() { },                                                           // GHN không được đụng vào
        [OrderStatus.Processing]         = new() { OrderStatus.Shipping, OrderStatus.Cancelled },               // Chuẩn bị → Đang giao / Hủy
        [OrderStatus.Shipping]           = new() { OrderStatus.Delivered, OrderStatus.Cancelled },              // Đang giao → Đã giao / Hủy
        [OrderStatus.Delivered]          = new() { OrderStatus.Completed, OrderStatus.Cancelled },              // Đã giao → Hoàn thành / Hủy (trường hợp trả hàng)
        [OrderStatus.Completed]          = new() { },                                                            // Đã xong — không đổi nữa
        [OrderStatus.Cancelled]          = new() { },                                                            // Đã hủy — không khôi phục từ GHN
        [OrderStatus.Refunded]           = new() { },                                                            // Đã hoàn tiền — không đổi nữa
    };

    private static bool IsAllowedTransition(OrderStatus from, OrderStatus to)
    {
        if (to == from) return true;
        return _allowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    /// <summary>
    /// Map mã <c>Status</c> từ GHN (List of shipping status) sang <see cref="OrderStatus"/>.
    /// Biến thể snake_case, không phân biệt hoa thường.
    /// </summary>
    private static OrderStatus? MapGhnStatusToOrderStatus(string? ghnStatus, string? type)
    {
        if (string.IsNullOrWhiteSpace(ghnStatus)) return null;
        var s = ghnStatus.Trim().ToLowerInvariant();

        return s switch
        {
            "ready_to_pick" or "picking" or "money_collect_picking" => OrderStatus.Processing,

            "picked" or "storing" or "transporting" or "sorting" => OrderStatus.Shipping,

            "transport"=> OrderStatus.Shipping,

            "delivering" => OrderStatus.Shipping,

            "delivered" => OrderStatus.Delivered,

            "cancel" or "canceled" or "cancelled" => OrderStatus.Cancelled,

            "lost" or "damage" => OrderStatus.Cancelled,

            "delivery_fail" or "not_deliver" or "not_delivered" => OrderStatus.Shipping,

            "waiting_to_return" or "return" or "return_transporting" or "return_sorting" or "returning" or "return_fail" =>
                OrderStatus.Shipping,

            "returned" => OrderStatus.Cancelled,

            "exception" or "fulfilling" or "on_process" or "pending" => OrderStatus.Shipping,

            _ => s.StartsWith("return", StringComparison.Ordinal) && s != "returned"
                ? OrderStatus.Shipping
                : GhnMapLegacy(s),
        };
    }

    private static OrderStatus? GhnMapLegacy(string s)
    {
        return s switch
        {
            "wait_to_return" => OrderStatus.Shipping,
            _ => null,
        };
    }

    private async Task NotifyStatusChangedAsync(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        // Thông báo cho khách hàng
        var customerGroup = OrderTrackingHub.GetUserGroupName(order.CustomerId);
        
        // Thông báo cho người bán (Shop owner)
        var sellerGroup = OrderTrackingHub.GetUserGroupName(order.Shop.OwnerId);

        var data = new
        {
            orderId = order.Id,
            oldStatus = (short)oldStatus,
            oldStatusName = OrderStatusVnHelper.Vietnamese(oldStatus),
            newStatus = (short)newStatus,
            newStatusName = OrderStatusVnHelper.Vietnamese(newStatus),
            updatedAt = order.UpdatedAt,
            source = "ghn"
        };

        await _hubContext.Clients.Groups(customerGroup, sellerGroup).SendAsync(
            "OrderStatusUpdated",
            data,
            cancellationToken: CancellationToken.None);
    }

}
