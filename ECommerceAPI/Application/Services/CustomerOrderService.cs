using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Orders;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Hubs;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class CustomerOrderService : ICustomerOrderService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<OrderTrackingHub> _hubContext;
    private readonly INotificationService _notifications;
    private readonly ISellerWalletReleaseService _walletRelease;
    private readonly IOrderNotificationEmailComposer _orderEmailComposer;

    public CustomerOrderService(
        ApplicationDbContext context,
        IHubContext<OrderTrackingHub> hubContext,
        INotificationService notifications,
        ISellerWalletReleaseService walletRelease,
        IOrderNotificationEmailComposer orderEmailComposer)
    {
        _context = context;
        _hubContext = hubContext;
        _notifications = notifications;
        _walletRelease = walletRelease;
        _orderEmailComposer = orderEmailComposer;
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
            ShopId = o.ShopId,
            ShopSlug = o.Shop.Slug,
            ShopName = o.Shop.Name,
            TotalAmount = o.Total,
            Status = o.Status,
            CreatedAt = o.CreatedAt,
            Items = o.OrderItems.Select(oi => new CustomerOrderItemDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
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
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.ProductImages)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Variant)
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

        var detail = new CustomerOrderDetailDto
        {
            Id = order.Id,
            OrderCode = order.OrderCode,
            ShopId = order.ShopId,
            ShopSlug = order.Shop.Slug,
            ShopName = order.Shop.Name,
            TotalAmount = order.Total,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            ShipFullName = order.ShipFullName,
            ShipPhone = order.ShipPhone,
            ShipAddress = order.ShipAddress,
            Items = order.OrderItems.Select(oi => new CustomerOrderItemDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
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
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

        if (order == null)
            return null;

        var statusEnum = (OrderStatus)order.Status;

        var steps = BuildTimeline(statusEnum, order);

        return new OrderTrackingDto
        {
            OrderId = order.Id,
            CurrentStatus = order.Status,
            CurrentStatusName = statusEnum.ToString(),
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

        if ((OrderStatus)order.Status != OrderStatus.Shipping)
        {
            return new ConfirmOrderResponseDto
            {
                Success = false,
                Message = "Chỉ có thể xác nhận đơn hàng đang giao (Shipping)"
            };
        }

        order.Status = (short)OrderStatus.Completed;
        order.UpdatedAt = DateTime.UtcNow;

        // Shipping → Completed: cộng SoldCount (chưa qua Delivered nên chưa được cộng trước đó)
        foreach (var item in order.OrderItems)
        {
            await _context.Products
                .Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.SoldCount, p => p.SoldCount + item.Quantity));
        }

        await _walletRelease.TryReleaseSettlementForOrderAsync(order.Id);

        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, OrderStatus.Shipping, OrderStatus.Completed);

        var code = NotificationFormatting.ShortEntityId(order.Id);
        var composed = await _orderEmailComposer.TryComposeAsync(
            order.Id,
            OrderStatus.Shipping,
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
            NewStatusName = OrderStatus.Completed.ToString(),
            UpdatedAt = order.UpdatedAt
        };
    }

    private static List<OrderStatusStepDto> BuildTimeline(OrderStatus currentStatus, Order order)
    {
        // Normal flow steps (theo đúng OrderStatus enum)
        var normalFlow = new List<OrderStatus>
        {
            OrderStatus.PendingPayment,
            OrderStatus.PendingConfirmation,
            OrderStatus.Confirmed,
            OrderStatus.Processing,
            OrderStatus.Shipping,
            OrderStatus.Delivered,
            OrderStatus.Completed,
        };

        // Terminal / out-of-flow statuses
        var terminalStatuses = new[]
        {
            OrderStatus.Cancelled,
            OrderStatus.Refunded,
        };

        var result = new List<OrderStatusStepDto>();

        // Build normal flow steps
        foreach (var status in normalFlow)
        {
            string state;

            if (currentStatus == OrderStatus.Cancelled || currentStatus == OrderStatus.Refunded)
            {
                // Order ended abnormally — mark all normal steps as cancelled
                state = "cancelled";
            }
            else if (currentStatus == status)
            {
                state = "current";
            }
            else if (currentStatus > status)
            {
                state = "completed";
            }
            else
            {
                state = "upcoming";
            }

            result.Add(new OrderStatusStepDto
            {
                Code = status.ToString(),
                DisplayName = status.ToString(),
                Value = (short)status,
                State = state,
                ReachedAt = state is "completed" or "current" ? order.UpdatedAt : null
            });
        }

        // Build terminal steps (Cancelled, Refunded)
        foreach (var status in terminalStatuses)
        {
            string state;
            if (currentStatus == status)
                state = "current";
            else if (currentStatus == OrderStatus.Cancelled || currentStatus == OrderStatus.Refunded)
                state = "upcoming";
            else
                state = "upcoming";

            result.Add(new OrderStatusStepDto
            {
                Code = status.ToString(),
                DisplayName = status.ToString(),
                Value = (short)status,
                State = state,
                ReachedAt = state == "current" ? order.UpdatedAt : null
            });
        }

        return result;
    }

    public async Task<ServiceResponse> CancelPendingOrderAsync(Guid customerId, Guid orderId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

        if (order == null)
            return new ServiceResponse { Success = false, Message = "Không tìm thấy đơn hàng" };

        if (order.Status != (short)OrderStatus.PendingPayment)
            return new ServiceResponse
            {
                Success = false,
                Message = "Chỉ có thể huỷ đơn hàng đang chờ thanh toán"
            };

        order.Status = (short)OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await NotifyStatusChanged(order, OrderStatus.PendingPayment, OrderStatus.Cancelled);

        return new ServiceResponse { Success = true, Message = "Đơn hàng đã được huỷ" };
    }

    private async Task NotifyStatusChanged(Order order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        var groupName = OrderTrackingHub.GetUserGroupName(order.CustomerId);

        await _hubContext.Clients.Group(groupName).SendAsync("OrderStatusUpdated", new
        {
            orderId = order.Id,
            oldStatus = (short)oldStatus,
            oldStatusName = oldStatus.ToString(),
            newStatus = (short)newStatus,
            newStatusName = newStatus.ToString(),
            updatedAt = order.UpdatedAt
        });
    }
}

