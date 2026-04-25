using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Orders;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Hubs;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECommerceAPI.Application.Services;

public class CustomerOrderService : ICustomerOrderService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<OrderTrackingHub> _hubContext;
    private readonly INotificationService _notifications;
    private readonly ISellerWalletReversalService _walletReversal;
    private readonly IOrderNotificationEmailComposer _orderEmailComposer;
    private readonly ICustomerWalletService _customerWallet;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IOrderStatusHistoryService _orderStatusHistory;

    public CustomerOrderService(
        ApplicationDbContext context,
        IHubContext<OrderTrackingHub> hubContext,
        INotificationService notifications,
        ISellerWalletReversalService walletReversal,
        IOrderNotificationEmailComposer orderEmailComposer,
        ICustomerWalletService customerWallet,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOrderStatusHistoryService orderStatusHistory)
    {
        _context = context;
        _hubContext = hubContext;
        _notifications = notifications;
        _walletReversal = walletReversal;
        _orderEmailComposer = orderEmailComposer;
        _customerWallet = customerWallet;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _orderStatusHistory = orderStatusHistory;
    }

    public async Task<CustomerOrderListResponseDto> GetMyOrdersAsync(Guid customerId, int page, int pageSize, short? status = null)
    {
        var query = _context.Orders
            .Include(o => o.Shop)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.ProductImages)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
            .Where(o => o.CustomerId == customerId);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var totalCount = await query.CountAsync();

        var rawOrders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var orderIds = rawOrders.Select(o => o.Id).ToList();
        var paymentProviderMap = orderIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _context.Payments
                .Where(p => orderIds.Contains(p.OrderId))
                .GroupBy(p => p.OrderId)
                .Select(g => new
                {
                    OrderId = g.Key,
                    Provider = g.OrderBy(p => p.CreatedAt).Select(p => p.Provider).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.OrderId, x => x.Provider);

        var allProductIds = rawOrders.SelectMany(o => o.OrderItems).Select(oi => oi.ProductId).Distinct().ToList();
        HashSet<Guid> reviewedProductIds;
        if (allProductIds.Count == 0)
            reviewedProductIds = new HashSet<Guid>();
        else
            reviewedProductIds = (await _context.ProductReviews
                .AsNoTracking()
                .Where(r => r.UserId == customerId && allProductIds.Contains(r.ProductId))
                .Select(r => r.ProductId)
                .ToListAsync())
                .ToHashSet();

        var orders = rawOrders.Select(o => new CustomerOrderSummaryDto
        {
            Id = o.Id,
            OrderCode = o.OrderCode,
            PaymentProvider = paymentProviderMap.TryGetValue(o.Id, out var provider) ? provider : null,
            CancelReason = o.CancelReason,
            ShopId = o.ShopId,
            ShopSlug = o.Shop.Slug,
            ShopName = o.Shop.Name,
            TotalAmount = o.Total,
            ShippingFee = o.ShippingFee,
            Status = o.Status,
            CreatedAt = o.CreatedAt,
            Items = o.OrderItems.Select(oi => new CustomerOrderItemDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
                VariantId = oi.VariantId,
                ProductName = oi.Product.Name,
                VariantName = oi.Variant?.VariantName,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                TotalPrice = oi.LineTotal,
                ThumbnailUrl = oi.Product.ProductImages.FirstOrDefault()?.ImageUrl,
                HasReviewedByUser = reviewedProductIds.Contains(oi.ProductId)
            }).ToList()
        }).ToList();

        return new CustomerOrderListResponseDto
        {
            Success = true,
            Orders = orders,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerOrderDetailResponseDto> GetOrderByIdAsync(Guid customerId, Guid orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Shop)
            .Include(o => o.OrderStatusHistories)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.ProductImages)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
            .Include(o => o.Shipments)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

        if (order == null)
        {
            return new CustomerOrderDetailResponseDto
            {
                Success = false,
                Message = "Không tìm thấy đơn hàng"
            };
        }

        var detailProductIds = order.OrderItems.Select(oi => oi.ProductId).Distinct().ToList();
        var detailReviewed = detailProductIds.Count == 0
            ? new HashSet<Guid>()
            : (await _context.ProductReviews
                .AsNoTracking()
                .Where(r => r.UserId == customerId && detailProductIds.Contains(r.ProductId))
                .Select(r => r.ProductId)
                .ToListAsync())
                .ToHashSet();

        var histories = order.OrderStatusHistories.OrderBy(x => x.CreatedAt).ToList();
        var shopOwnerId = order.Shop?.OwnerId;

        // Lấy vận đơn mới nhất để hiển thị thông tin giao hàng cho customer
        var latestShipment = order.Shipments?
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        var detail = new CustomerOrderDetailDto
        {
            Id = order.Id,
            OrderCode = order.OrderCode,
            PaymentProvider = await _context.Payments
                .Where(p => p.OrderId == order.Id)
                .OrderBy(p => p.CreatedAt)
                .Select(p => p.Provider)
                .FirstOrDefaultAsync(),
            CancelReason = order.CancelReason,
            ShopId = order.ShopId,
            ShopSlug = order.Shop.Slug,
            ShopName = order.Shop.Name,
            TotalAmount = order.Total,
            ShippingFee = order.ShippingFee,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            StatusHistory = OrderStatusTimelineBuilder.MapHistory(histories, order.CustomerId, shopOwnerId, forSellerView: false),
            StatusTimeline = OrderStatusTimelineBuilder.BuildSteps((OrderStatus)order.Status, order, histories),
            ShipFullName = order.ShipFullName,
            ShipPhone = PhoneVnHelper.NormalizeToLocal(order.ShipPhone) ?? order.ShipPhone,
            ShipAddress = order.ShipAddress,
            EstimatedDeliveryDate = latestShipment?.EstimatedDeliveryDate,
            ActualDeliveryDate = latestShipment?.ActualDeliveryDate,
            TrackingCode = latestShipment?.TrackingCode,
            ShippingProvider = latestShipment?.ShippingProvider,
            Items = order.OrderItems.Select(oi => new CustomerOrderItemDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
                VariantId = oi.VariantId,
                ProductName = oi.Product.Name,
                VariantName = oi.Variant?.VariantName,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                TotalPrice = oi.LineTotal,
                ThumbnailUrl = oi.Product.ProductImages.FirstOrDefault()?.ImageUrl,
                HasReviewedByUser = detailReviewed.Contains(oi.ProductId)
            }).ToList()
        };

        return new CustomerOrderDetailResponseDto
        {
            Success = true,
            Order = detail
        };
    }

    public async Task<OrderTrackingDto?> GetOrderTrackingAsync(Guid customerId, Guid orderId)
    {
        var order = await _context.Orders
            .Include(o => o.OrderStatusHistories)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

        if (order == null)
            return null;

        var statusEnum = (OrderStatus)order.Status;
        var historyList = order.OrderStatusHistories.OrderBy(x => x.CreatedAt).ToList();
        var steps = OrderStatusTimelineBuilder.BuildSteps(statusEnum, order, historyList);

        return new OrderTrackingDto
        {
            OrderId = order.Id,
            CurrentStatus = order.Status,
            CurrentStatusName = OrderStatusVnHelper.Vietnamese(statusEnum),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            Timeline = steps
        };
    }

    public async Task<ConfirmOrderResponseDto> ConfirmOrderAsync(Guid customerId, Guid orderId)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

        if (order == null)
        {
            return new ConfirmOrderResponseDto
            {
                Success = false,
                Message = "Không tìm thấy đơn hàng"
            };
        }

        if ((OrderStatus)order.Status != OrderStatus.Delivered)
        {
            return new ConfirmOrderResponseDto
            {
                Success = false,
                Message = "Chỉ có thể xác nhận khi đơn đã giao hàng (Delivered). Vui lòng chờ shop cập nhật trạng thái."
            };
        }

        var deliverAnchor = await OrderPostDeliveryHelper.GetDeliveryAnchorUtcAsync(
            _context,
            order.Id,
            order.UpdatedAt);
        if ((DateTime.UtcNow - deliverAnchor).TotalDays < SellerWalletLedgerPolicies.ReleaseDaysAfterOrderDelivered)
        {
            return new ConfirmOrderResponseDto
            {
                Success = false,
                Message =
                    $"Chỉ có thể xác nhận hoàn thành sau {SellerWalletLedgerPolicies.ReleaseDaysAfterOrderDelivered} ngày kể từ khi đơn đã giao (hết thời hạn khiếu nại)."
            };
        }

        if (await OrderPostDeliveryHelper.HasOpenDisputeAsync(_context, order.Id))
        {
            return new ConfirmOrderResponseDto
            {
                Success = false,
                Message = "Đơn đang có khiếu nại chưa kết thúc. Không thể xác nhận hoàn thành."
            };
        }

        order.Status = (short)OrderStatus.Completed;
        order.UpdatedAt = DateTime.UtcNow;
        _orderStatusHistory.AddEntry(
            order.Id,
            (short)OrderStatus.Delivered,
            (short)OrderStatus.Completed,
            customerId,
            "Khách xác nhận đã nhận hàng");

        // Delivered → Completed: cộng SoldCount (chưa cộng khi ở Delivered)
        foreach (var item in order.OrderItems)
        {
            await _context.Products
                .Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.SoldCount, p => p.SoldCount + item.Quantity));
        }

        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, OrderStatus.Delivered, OrderStatus.Completed);

        var code = NotificationFormatting.ShortEntityId(order.Id);
        var composed = await _orderEmailComposer.TryComposeAsync(
            order.Id,
            OrderStatus.Delivered,
            OrderStatus.Completed);
        await _notifications.PublishAsync(
            order.CustomerId,
            nameof(NotificationType.Order),
            "Đơn hàng đã hoàn thành",
            $"Bạn đã xác nhận nhận hàng cho đơn #{code}.",
            "Order",
            order.Id,
            queueEmail: true,
            emailHtmlBody: composed?.Html,
            emailSubjectOverride: composed?.Subject);

        return new ConfirmOrderResponseDto
        {
            Success = true,
            Message = "Xác nhận đã nhận hàng thành công",
            OrderId = order.Id,
            NewStatus = order.Status,
            NewStatusName = OrderStatusVnHelper.Vietnamese(OrderStatus.Completed),
            UpdatedAt = order.UpdatedAt
        };
    }

    public async Task<int> AutoCompleteDeliveredOrdersPastDisputeWindowAsync(
        CancellationToken cancellationToken = default)
    {
        var deliveredIds = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Status == (short)OrderStatus.Delivered)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var n = 0;
        foreach (var id in deliveredIds)
        {
            if (await TryAutoCompleteDeliveredOrderAsync(id, cancellationToken))
                n++;
        }

        return n;
    }

    /// <summary>
    /// Hoàn thành đơn khi đã Đã giao đủ ngày và không còn khiếu nại mở (đồng bộ nghiệp vụ rút tiền seller).
    /// </summary>
    private async Task<bool> TryAutoCompleteDeliveredOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.Status == (short)OrderStatus.Delivered, cancellationToken);

        if (order == null)
            return false;

        var anchor = await OrderPostDeliveryHelper.GetDeliveryAnchorUtcAsync(
            _context,
            order.Id,
            order.UpdatedAt,
            cancellationToken);

        if ((DateTime.UtcNow - anchor).TotalDays < SellerWalletLedgerPolicies.ReleaseDaysAfterOrderDelivered)
            return false;

        if (await OrderPostDeliveryHelper.HasOpenDisputeAsync(_context, order.Id, cancellationToken))
            return false;

        order.Status = (short)OrderStatus.Completed;
        order.UpdatedAt = DateTime.UtcNow;
        _orderStatusHistory.AddEntry(
            order.Id,
            (short)OrderStatus.Delivered,
            (short)OrderStatus.Completed,
            null,
            "Tự động hoàn thành sau thời hạn khiếu nại (không còn khiếu nại mở)");

        foreach (var item in order.OrderItems)
        {
            await _context.Products
                .Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(p => p.SoldCount, p => p.SoldCount + item.Quantity),
                    cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await NotifyStatusChanged(order, OrderStatus.Delivered, OrderStatus.Completed);

        var code = NotificationFormatting.ShortEntityId(order.Id);
        var composed = await _orderEmailComposer.TryComposeAsync(
            order.Id,
            OrderStatus.Delivered,
            OrderStatus.Completed);
        await _notifications.PublishAsync(
            order.CustomerId,
            nameof(NotificationType.Order),
            "Đơn hàng đã hoàn thành",
            $"Đơn #{code} đã tự động hoàn thành sau thời hạn khiếu nại.",
            "Order",
            order.Id,
            queueEmail: true,
            emailHtmlBody: composed?.Html,
            emailSubjectOverride: composed?.Subject);

        return true;
    }

    public Task<ServiceResponse> CancelOrderAsync(Guid customerId, Guid orderId, string? reason = null)
    {
        return CancelOrderCoreAsync(customerId, orderId, reason, pendingOnly: false);
    }

    public Task<ServiceResponse> CancelPendingOrderAsync(Guid customerId, Guid orderId, string? reason = null)
    {
        return CancelOrderCoreAsync(customerId, orderId, reason, pendingOnly: true);
    }

    private async Task<ServiceResponse> CancelOrderCoreAsync(Guid customerId, Guid orderId, string? reason, bool pendingOnly)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.Payments)
            .Include(o => o.Shipments)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

        if (order == null)
            return new ServiceResponse { Success = false, Message = "Không tìm thấy đơn hàng" };

        var oldStatus = (OrderStatus)order.Status;
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim()[..Math.Min(reason.Trim().Length, 500)];

        var canCancel = pendingOnly
            ? oldStatus == OrderStatus.PendingPayment
            : oldStatus is OrderStatus.PendingPayment
                or OrderStatus.PendingConfirmation
                or OrderStatus.Confirmed
                or OrderStatus.Processing;

        if (!canCancel)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = pendingOnly
                    ? "Chỉ có thể huỷ đơn hàng đang chờ thanh toán"
                    : "Chỉ có thể huỷ đơn hàng trước khi giao"
            };
        }

        if (!pendingOnly && oldStatus == OrderStatus.Processing)
        {
            var ghnCancel = await CancelGhnOrderIfRequiredAsync(order);
            if (!ghnCancel.Success)
            {
                return ghnCancel;
            }
        }

        var now = DateTime.UtcNow;
        var hasPaidPayment = order.Payments.Any(p => p.Status == (short)PaymentStatus.Paid);

        foreach (var item in order.OrderItems)
        {
            var inv = await _context.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.VariantId == item.VariantId);

            if (inv == null)
                continue;

            if (hasPaidPayment)
            {
                inv.Quantity += item.Quantity;
            }

            inv.ReservedQuantity = Math.Max(0, inv.ReservedQuantity - item.Quantity);
            inv.UpdatedAt = now;
        }

        foreach (var payment in order.Payments.Where(p => p.Status == (short)PaymentStatus.Pending))
        {
            payment.Status = (short)PaymentStatus.Cancelled;
            payment.PaidAt = now;
        }

        order.Status = (short)OrderStatus.Cancelled;
        order.CancelReason = normalizedReason;
        order.UpdatedAt = now;
        _orderStatusHistory.AddEntry(
            order.Id,
            (short)oldStatus,
            (short)OrderStatus.Cancelled,
            customerId,
            string.IsNullOrWhiteSpace(normalizedReason) ? "Khách hủy đơn" : $"Khách hủy đơn: {normalizedReason}");

        decimal paidAmount = 0;
        if (hasPaidPayment)
        {
            await _walletReversal.TryReverseSettlementForOrderAsync(
                order.Id,
                string.IsNullOrWhiteSpace(normalizedReason)
                    ? "Customer huỷ đơn trước khi giao hàng"
                    : $"Customer huỷ đơn: {normalizedReason}");

            paidAmount = order.Payments
                .Where(p => p.Status == (short)PaymentStatus.Paid)
                .Sum(p => p.Amount);
        }

        await _context.SaveChangesAsync();

        if (paidAmount > 0)
        {
            var orderCode = string.IsNullOrWhiteSpace(order.OrderCode)
                ? NotificationFormatting.ShortEntityId(order.Id)
                : order.OrderCode;
            await _customerWallet.CreditRefundAsync(
                order.CustomerId,
                paidAmount,
                "Order",
                order.Id,
                $"Hoàn tiền đơn #{orderCode} bị huỷ");
        }

        await NotifyStatusChanged(order, oldStatus, OrderStatus.Cancelled);

        var code = string.IsNullOrWhiteSpace(order.OrderCode)
            ? NotificationFormatting.ShortEntityId(order.Id)
            : order.OrderCode;
        var reasonPart = string.IsNullOrWhiteSpace(normalizedReason) ? string.Empty : $" Lý do: {normalizedReason}";
        var composed = await _orderEmailComposer.TryComposeAsync(order.Id, oldStatus, OrderStatus.Cancelled);
        await _notifications.PublishAsync(
            order.CustomerId,
            nameof(NotificationType.Order),
            "Đơn hàng đã được huỷ",
            $"Đơn #{code} đã được huỷ thành công.{reasonPart}",
            "Order",
            order.Id,
            queueEmail: true,
            emailHtmlBody: composed?.Html,
            emailSubjectOverride: composed?.Subject);

        return new ServiceResponse { Success = true, Message = "Đơn hàng đã được huỷ" };
    }

    private async Task<ServiceResponse> CancelGhnOrderIfRequiredAsync(Order order)
    {
        if (order.Shipments is not { } list || !list.Any(s => string.Equals(s.ShippingProvider, "GHN", StringComparison.OrdinalIgnoreCase)))
        {
            return new ServiceResponse { Success = true };
        }

        var trackingCode = OrderShipmentHelper.GhnTrackingOrNull(order.Shipments);
        if (string.IsNullOrWhiteSpace(trackingCode))
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Không thể huỷ đơn GHN vì thiếu mã vận đơn (tracking code)."
            };
        }

        var ghnToken = (_configuration["GHN:Token"] ?? _configuration["NEXT_PUBLIC_GHN_TOKEN"])?.Trim();
        var ghnBaseUrl = (_configuration["GHN:BaseUrl"] ?? "https://dev-online-gateway.ghn.vn").TrimEnd('/');

        if (string.IsNullOrWhiteSpace(ghnToken))
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Thiếu cấu hình GHN (Token), không thể huỷ vận đơn."
            };
        }

        var shop = await _context.Shops.FirstOrDefaultAsync(s => s.Id == order.ShopId);
        if (shop?.GhnShopId == null)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = "Shop chưa đăng ký GHN Shop ID, không thể huỷ vận đơn."
            };
        }

        var ghnShopId = shop.GhnShopId.Value.ToString();

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{ghnBaseUrl}/shiip/public-api/v2/switch-status/cancel");

        request.Headers.TryAddWithoutValidation("Token", ghnToken);
        request.Headers.TryAddWithoutValidation("ShopId", ghnShopId);
        request.Content = JsonContent.Create(new { order_codes = new[] { trackingCode } });

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request);
        }
        catch (Exception ex)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = $"Không thể kết nối GHN để huỷ vận đơn: {ex.Message}"
            };
        }

        var responseText = await response.Content.ReadAsStringAsync();
        GhnCancelResponse? ghn;
        try
        {
            ghn = JsonSerializer.Deserialize<GhnCancelResponse>(
                responseText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            ghn = null;
        }

        if (!response.IsSuccessStatusCode || ghn?.Code != 200)
        {
            var message = GhnApiErrorText.FromResponseBody(responseText, (int)response.StatusCode);

            return new ServiceResponse
            {
                Success = false,
                Message = $"Huỷ vận đơn GHN thất bại: {message}"
            };
        }

        var item = ghn.Data?.FirstOrDefault(x => string.Equals(x.OrderCode, trackingCode, StringComparison.OrdinalIgnoreCase));
        if (item is null || !item.Result)
        {
            return new ServiceResponse
            {
                Success = false,
                Message = $"Huỷ vận đơn GHN thất bại: {item?.Message ?? "Không nhận được kết quả huỷ hợp lệ."}"
            };
        }

        return new ServiceResponse { Success = true };
    }

    private sealed class GhnCancelResponse
    {
        public int Code { get; set; }
        public string? Message { get; set; }
        public List<GhnCancelOrderResult>? Data { get; set; }
    }

    private sealed class GhnCancelOrderResult
    {
        [JsonPropertyName("order_code")]
        public string? OrderCode { get; set; }

        [JsonPropertyName("result")]
        public bool Result { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private async Task NotifyStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        var groupName = OrderTrackingHub.GetUserGroupName(order.CustomerId);

        await _hubContext.Clients.Group(groupName).SendAsync("OrderStatusUpdated", new
        {
            orderId = order.Id,
            oldStatus = (short)oldStatus,
            oldStatusName = OrderStatusVnHelper.Vietnamese(oldStatus),
            newStatus = (short)newStatus,
            newStatusName = OrderStatusVnHelper.Vietnamese(newStatus),
            updatedAt = order.UpdatedAt
        });
    }
}

