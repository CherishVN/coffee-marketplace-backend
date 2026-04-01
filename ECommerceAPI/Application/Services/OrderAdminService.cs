using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Hubs;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class OrderAdminService : IOrderAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrderAdminService> _logger;
    private readonly IHubContext<OrderTrackingHub> _hubContext;
    private readonly INotificationService _notifications;
    private readonly ISellerWalletReversalService _walletReversal;

    public OrderAdminService(
        ApplicationDbContext context,
        ILogger<OrderAdminService> logger,
        IHubContext<OrderTrackingHub> hubContext,
        INotificationService notifications,
        ISellerWalletReversalService walletReversal)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
        _notifications = notifications;
        _walletReversal = walletReversal;
    }

    public async Task<AdminOrderListResponseDto> GetAllOrdersAsync(
        int page,
        int pageSize,
        short? status = null,
        Guid? shopId = null,
        Guid? customerId = null,
        string? search = null)
    {
        try
        {
            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Shop)
                .Include(o => o.OrderItems)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            if (shopId.HasValue)
            {
                query = query.Where(o => o.ShopId == shopId.Value);
            }

            if (customerId.HasValue)
            {
                query = query.Where(o => o.CustomerId == customerId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(o =>
                    o.Id.ToString().ToLower().Contains(searchLower) ||
                    (o.ShipFullName != null && o.ShipFullName.ToLower().Contains(searchLower)) ||
                    (o.ShipPhone != null && o.ShipPhone.Contains(search)) ||
                    o.Customer.FullName.ToLower().Contains(searchLower) ||
                    o.Shop.Name.ToLower().Contains(searchLower));
            }

            var totalCount = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new AdminOrderDto
                {
                    Id = o.Id,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer.FullName ?? o.Customer.Id.ToString(),
                    CustomerEmail = null,
                    ShopId = o.ShopId,
                    ShopName = o.Shop.Name,
                    Status = o.Status,
                    StatusName = ((OrderStatus)o.Status).ToString(),
                    Subtotal = o.Subtotal,
                    ShippingFee = o.ShippingFee,
                    Total = o.Total,
                    ShipFullName = o.ShipFullName,
                    ShipPhone = o.ShipPhone,
                    ShipAddress = o.ShipAddress,
                    ItemCount = o.OrderItems.Count,
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt,
                })
                .ToListAsync();

            return new AdminOrderListResponseDto
            {
                Success = true,
                Orders = orders,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders");
            return new AdminOrderListResponseDto
            {
                Success = false,
                Message = "Lỗi khi tải danh sách đơn hàng",
            };
        }
    }

    public async Task<AdminOrderResponseDto> GetOrderByIdAsync(Guid orderId)
    {
        try
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Shop)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return new AdminOrderResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy đơn hàng",
                };
            }

            return new AdminOrderResponseDto
            {
                Success = true,
                Order = new AdminOrderDetailDto
                {
                    Id = order.Id,
                    CustomerId = order.CustomerId,
                    CustomerName = order.Customer.FullName ?? order.Customer.Id.ToString(),
                    CustomerEmail = null,
                    ShopId = order.ShopId,
                    ShopName = order.Shop.Name,
                    Status = order.Status,
                    StatusName = ((OrderStatus)order.Status).ToString(),
                    Subtotal = order.Subtotal,
                    ShippingFee = order.ShippingFee,
                    Total = order.Total,
                    ShipFullName = order.ShipFullName,
                    ShipPhone = order.ShipPhone,
                    ShipAddress = order.ShipAddress,
                    ItemCount = order.OrderItems.Count,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt,
                    Items = order.OrderItems.Select(oi => new OrderItemDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        ProductName = oi.ProductName,
                        UnitPrice = oi.UnitPrice,
                        Quantity = oi.Quantity,
                        LineTotal = oi.LineTotal,
                    }).ToList(),
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order {OrderId}", orderId);
            return new AdminOrderResponseDto
            {
                Success = false,
                Message = "Lỗi khi tải chi tiết đơn hàng",
            };
        }
    }

    public async Task<AdminOrderResponseDto> UpdateOrderStatusAsync(
        Guid orderId, UpdateOrderStatusDto dto, Guid adminId)
    {
        try
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Shop)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return new AdminOrderResponseDto
                {
                    Success = false,
                    Message = "Không tìm thấy đơn hàng",
                };
            }

            if (!Enum.IsDefined(typeof(OrderStatus), dto.NewStatus))
            {
                return new AdminOrderResponseDto
                {
                    Success = false,
                    Message = "Trạng thái không hợp lệ",
                };
            }

            var oldStatus = (OrderStatus)order.Status;
            var newOrderStatus = (OrderStatus)dto.NewStatus;
            order.Status = dto.NewStatus;
            order.UpdatedAt = DateTime.UtcNow;

            // Cộng SoldCount khi đơn lần đầu đạt Completed(6) — khách xác nhận nhận hàng
            var alreadyFulfilled = oldStatus == OrderStatus.Completed;
            var nowFulfilled = newOrderStatus == OrderStatus.Completed;
            if (nowFulfilled && !alreadyFulfilled)
            {
                foreach (var item in order.OrderItems)
                {
                    await _context.Products
                        .Where(p => p.Id == item.ProductId)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.SoldCount, p => p.SoldCount + item.Quantity));
                }
            }

            if (newOrderStatus is OrderStatus.Cancelled or OrderStatus.Refunded)
            {
                await _walletReversal.TryReverseSettlementForOrderAsync(
                    order.Id,
                    string.IsNullOrWhiteSpace(dto.Reason)
                        ? $"Admin → {newOrderStatus}"
                        : dto.Reason);
            }

            await _context.SaveChangesAsync();

            await NotifyStatusChanged(order, oldStatus, (OrderStatus)order.Status);

            var newStatus = (OrderStatus)order.Status;
            var code = NotificationFormatting.ShortEntityId(order.Id);
            var reasonPart = string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" Ghi chú: {dto.Reason}";
            await _notifications.PublishAsync(
                order.CustomerId,
                nameof(NotificationType.Order),
                "Cập nhật đơn hàng (Admin)",
                $"Đơn #{code} được cập nhật trạng thái: {newStatus}.{reasonPart}",
                "Order",
                order.Id,
                queueEmail: true);

            _logger.LogInformation(
                "Admin {AdminId} updated order {OrderId} status to {Status}. Reason: {Reason}",
                adminId, orderId, (OrderStatus)dto.NewStatus, dto.Reason);

            return new AdminOrderResponseDto
            {
                Success = true,
                Message = $"Cập nhật trạng thái đơn hàng thành '{((OrderStatus)dto.NewStatus).ToString()}'",
                Order = new AdminOrderDetailDto
                {
                    Id = order.Id,
                    CustomerId = order.CustomerId,
                    CustomerName = order.Customer.FullName ?? order.Customer.Id.ToString(),
                    CustomerEmail = null,
                    ShopId = order.ShopId,
                    ShopName = order.Shop.Name,
                    Status = order.Status,
                    StatusName = ((OrderStatus)order.Status).ToString(),
                    Subtotal = order.Subtotal,
                    ShippingFee = order.ShippingFee,
                    Total = order.Total,
                    ShipFullName = order.ShipFullName,
                    ShipPhone = order.ShipPhone,
                    ShipAddress = order.ShipAddress,
                    ItemCount = order.OrderItems.Count,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt,
                    Items = order.OrderItems.Select(oi => new OrderItemDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        ProductName = oi.ProductName,
                        UnitPrice = oi.UnitPrice,
                        Quantity = oi.Quantity,
                        LineTotal = oi.LineTotal,
                    }).ToList(),
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId} status", orderId);
            return new AdminOrderResponseDto
            {
                Success = false,
                Message = "Lỗi khi cập nhật trạng thái đơn hàng",
            };
        }
    }

    private async Task NotifyStatusChanged(Domain.Entities.Order order, OrderStatus oldStatus, OrderStatus newStatus)
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
