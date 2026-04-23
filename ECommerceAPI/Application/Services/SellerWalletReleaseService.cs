using ECommerceAPI.Application;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class SellerWalletReleaseService : ISellerWalletReleaseService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SellerWalletReleaseService> _logger;

    public SellerWalletReleaseService(
        ApplicationDbContext context,
        ILogger<SellerWalletReleaseService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> TryReleaseSettlementForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var alreadyReleased = await _context.SellerWalletLedgers
            .AnyAsync(
                l => l.ReferenceType == WalletLedgerReferenceTypes.OrderRelease && l.ReferenceId == orderId,
                cancellationToken);

        if (alreadyReleased)
            return false;

        var settlement = await _context.SellerWalletLedgers
            .FirstOrDefaultAsync(
                l => l.ReferenceType == WalletLedgerReferenceTypes.OrderSettlement
                     && l.ReferenceId == orderId,
                cancellationToken);

        if (settlement == null || settlement.Amount <= 0)
            return false;

        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null || order.Status != (short)OrderStatus.Completed)
            return false;

        if (await OrderPostDeliveryHelper.HasOpenDisputeAsync(_context, orderId, cancellationToken))
            return false;

        var anchorUtc = await OrderPostDeliveryHelper.GetDeliveryAnchorUtcAsync(
            _context,
            orderId,
            order.UpdatedAt,
            cancellationToken);

        if ((DateTime.UtcNow - anchorUtc).TotalDays < SellerWalletLedgerPolicies.ReleaseDaysAfterOrderDelivered)
            return false;

        var net = settlement.Amount;

        var wallet = await _context.SellerWallets
            .FirstOrDefaultAsync(w => w.Id == settlement.WalletId, cancellationToken);

        if (wallet == null)
        {
            _logger.LogError("Release: wallet {WalletId} not found for order {OrderId}", settlement.WalletId, orderId);
            return false;
        }

        if (wallet.HeldBalance >= net)
        {
            wallet.HeldBalance -= net;
            wallet.AvailableBalance += net;
        }
        else
        {
            _logger.LogWarning(
                "Order {OrderId} release: held {Held} < net {Net} — ghi nhận giải ngân (dữ liệu cũ / migration), không điều chỉnh số dư",
                orderId, wallet.HeldBalance, net);
        }

        wallet.UpdatedAt = DateTime.UtcNow;

        await _context.SellerWalletLedgers.AddAsync(
            new SellerWalletLedger
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                Type = "credit",
                Amount = 0,
                Currency = wallet.Currency,
                ReferenceType = WalletLedgerReferenceTypes.OrderRelease,
                ReferenceId = orderId,
                Note =
                    $"Giải ngân sau khi đơn hoàn thành (≥{SellerWalletLedgerPolicies.ReleaseDaysAfterOrderDelivered} ngày từ Đã giao, không khiếu nại mở), net {net:N0} {wallet.Currency}.",
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken);

        _logger.LogInformation("Released settlement for order {OrderId}, net {Net}", orderId, net);

        return true;
    }

    public async Task<int> ReleaseDueSettlementsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-SellerWalletLedgerPolicies.ReleaseDaysAfterOrderDelivered);

        var settlementOrderIds = await _context.SellerWalletLedgers
            .AsNoTracking()
            .Where(l => l.ReferenceType == WalletLedgerReferenceTypes.OrderSettlement && l.Amount > 0)
            .Select(l => l.ReferenceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (settlementOrderIds.Count == 0)
            return 0;

        var releasedList = await _context.SellerWalletLedgers
            .AsNoTracking()
            .Where(l =>
                l.ReferenceType == WalletLedgerReferenceTypes.OrderRelease
                && settlementOrderIds.Contains(l.ReferenceId))
            .Select(l => l.ReferenceId)
            .ToListAsync(cancellationToken);
        var releasedSet = releasedList.ToHashSet();

        var pending = settlementOrderIds.Where(id => !releasedSet.Contains(id)).ToList();
        if (pending.Count == 0)
            return 0;

        var eligibleRows = await _context.Orders
            .AsNoTracking()
            .Where(o => pending.Contains(o.Id) && o.Status == (short)OrderStatus.Completed)
            .Select(o => new { o.Id, o.UpdatedAt })
            .ToListAsync(cancellationToken);

        if (eligibleRows.Count == 0)
            return 0;

        var eligibleIds = eligibleRows.Select(r => r.Id).ToList();

        var firstDeliveredRows = await _context.OrderStatusHistories
            .AsNoTracking()
            .Where(h => eligibleIds.Contains(h.OrderId) && h.NewStatus == (short)OrderStatus.Delivered)
            .GroupBy(h => h.OrderId)
            .Select(g => new { OrderId = g.Key, FirstAt = g.Min(h => h.CreatedAt) })
            .ToListAsync(cancellationToken);

        var firstCompletedRows = await _context.OrderStatusHistories
            .AsNoTracking()
            .Where(h => eligibleIds.Contains(h.OrderId) && h.NewStatus == (short)OrderStatus.Completed)
            .GroupBy(h => h.OrderId)
            .Select(g => new { OrderId = g.Key, FirstAt = g.Min(h => h.CreatedAt) })
            .ToListAsync(cancellationToken);

        var delMap = firstDeliveredRows.ToDictionary(x => x.OrderId, x => x.FirstAt);
        var compMap = firstCompletedRows.ToDictionary(x => x.OrderId, x => x.FirstAt);

        var ordersWithOpenDispute = await _context.Disputes
            .AsNoTracking()
            .Where(d =>
                eligibleIds.Contains(d.OrderId)
                && d.Status != (short)DisputeStatus.Resolved
                && d.Status != (short)DisputeStatus.Rejected
                && d.Status != (short)DisputeStatus.Refunded
                && d.Status != (short)DisputeStatus.Cancelled)
            .Select(d => d.OrderId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var openDisputeSet = ordersWithOpenDispute.ToHashSet();

        var dueIds = eligibleRows
            .Where(row =>
            {
                if (openDisputeSet.Contains(row.Id))
                    return false;
                var deliverAnchor = delMap.TryGetValue(row.Id, out var d)
                    ? d
                    : compMap.TryGetValue(row.Id, out var c)
                        ? c
                        : row.UpdatedAt;
                return deliverAnchor <= cutoff;
            })
            .Select(row => row.Id)
            .ToList();

        var n = 0;
        foreach (var oid in dueIds)
        {
            if (await TryReleaseSettlementForOrderAsync(oid, cancellationToken))
                n++;
        }

        return n;
    }
}
