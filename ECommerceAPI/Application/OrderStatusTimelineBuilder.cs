using ECommerceAPI.Application.DTOs.Orders;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;

namespace ECommerceAPI.Application;

public static class OrderStatusTimelineBuilder
{
    public static string GetStatusDisplayNameVn(short status) => (OrderStatus)status switch
    {
        OrderStatus.PendingPayment => "Chờ thanh toán",
        OrderStatus.PendingConfirmation => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Processing => "Đang chuẩn bị",
        OrderStatus.Shipping => "Đang giao hàng",
        OrderStatus.Delivered => "Đã giao hàng",
        OrderStatus.Completed => "Hoàn thành",
        OrderStatus.Cancelled => "Đã hủy",
        OrderStatus.Refunded => "Đã hoàn tiền",
        _ => ((OrderStatus)status).ToString()
    };

    public static List<OrderStatusStepDto> BuildSteps(
        OrderStatus currentStatus,
        Order order,
        IReadOnlyList<OrderStatusHistory>? histories = null)
    {
        var lastReached = new Dictionary<short, DateTime>();
        if (histories is { Count: > 0 })
        {
            foreach (var h in histories.OrderBy(x => x.CreatedAt))
                lastReached[(short)h.NewStatus] = h.CreatedAt;
        }

        DateTime? ReachedFor(short value, string state)
        {
            if (state is not ("completed" or "current"))
                return null;
            if (lastReached.TryGetValue(value, out var t))
                return t;
            return state == "current" ? order.UpdatedAt : null;
        }

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

        var terminalStatuses = new[] { OrderStatus.Cancelled, OrderStatus.Refunded };

        var result = new List<OrderStatusStepDto>();

        foreach (var status in normalFlow)
        {
            string state;
            if (currentStatus is OrderStatus.Cancelled or OrderStatus.Refunded)
                state = "cancelled";
            else if (currentStatus == status)
                state = "current";
            else if (currentStatus > status)
                state = "completed";
            else
                state = "upcoming";

            var v = (short)status;
            result.Add(new OrderStatusStepDto
            {
                Code = status.ToString(),
                DisplayName = GetStatusDisplayNameVn(v),
                Value = v,
                State = state,
                ReachedAt = ReachedFor(v, state)
            });
        }

        foreach (var status in terminalStatuses)
        {
            string state;
            if (currentStatus == status)
                state = "current";
            else
                state = "upcoming";

            var v = (short)status;
            result.Add(new OrderStatusStepDto
            {
                Code = status.ToString(),
                DisplayName = GetStatusDisplayNameVn(v),
                Value = v,
                State = state,
                ReachedAt = ReachedFor(v, state)
            });
        }

        return result;
    }

    public static List<OrderStatusHistoryItemDto> MapHistory(
        IReadOnlyList<OrderStatusHistory> rows,
        Guid customerId,
        Guid? shopOwnerId,
        bool forSellerView)
    {
        return rows
            .OrderBy(r => r.CreatedAt)
            .Select(h => new OrderStatusHistoryItemDto
            {
                PreviousStatus = h.PreviousStatus,
                PreviousStatusLabel = h.PreviousStatus.HasValue
                    ? GetStatusDisplayNameVn(h.PreviousStatus.Value)
                    : null,
                NewStatus = h.NewStatus,
                NewStatusLabel = GetStatusDisplayNameVn(h.NewStatus),
                CreatedAt = h.CreatedAt,
                Note = h.Note,
                Source = ResolveSource(h, customerId, shopOwnerId, forSellerView)
            })
            .ToList();
    }

    private static string ResolveSource(OrderStatusHistory h, Guid customerId, Guid? shopOwnerId, bool forSellerView)
    {
        if (h.ChangedBy == null)
            return "system";
        if (h.ChangedBy == customerId)
            return "customer";
        if (shopOwnerId.HasValue && h.ChangedBy == shopOwnerId.Value)
            return "shop";
        return forSellerView ? "other" : "shop";
    }
}
