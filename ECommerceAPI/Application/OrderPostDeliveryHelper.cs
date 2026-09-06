using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application;

/// <summary>Mốc giao hàng / khiếu nại dùng chung cho ví seller và hoàn thành đơn.</summary>
public static class OrderPostDeliveryHelper
{
    private static readonly HashSet<short> TerminalDisputeStatuses =
    [
        (short)DisputeStatus.Resolved,
        (short)DisputeStatus.Rejected,
        (short)DisputeStatus.Refunded,
        (short)DisputeStatus.Cancelled
    ];

    /// <summary>Ưu tiên lần đầu Delivered; không có thì lần đầu Completed; không có lịch sử thì <paramref name="orderUpdatedAt"/>.</summary>
    public static async Task<DateTime> GetDeliveryAnchorUtcAsync(
        ApplicationDbContext context,
        Guid orderId,
        DateTime orderUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        var firstDelivered = await context.OrderStatusHistories
            .AsNoTracking()
            .Where(h => h.OrderId == orderId && h.NewStatus == (short)OrderStatus.Delivered)
            .OrderBy(h => h.CreatedAt)
            .Select(h => (DateTime?)h.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstDelivered.HasValue)
            return firstDelivered.Value;

        var firstCompleted = await context.OrderStatusHistories
            .AsNoTracking()
            .Where(h => h.OrderId == orderId && h.NewStatus == (short)OrderStatus.Completed)
            .OrderBy(h => h.CreatedAt)
            .Select(h => (DateTime?)h.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return firstCompleted ?? orderUpdatedAt;
    }

    public static Task<bool> HasOpenDisputeAsync(
        ApplicationDbContext context,
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        context.Disputes.AsNoTracking()
            .AnyAsync(d => d.OrderId == orderId && !TerminalDisputeStatuses.Contains(d.Status), cancellationToken);
}
