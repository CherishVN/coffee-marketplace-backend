using ECommerceAPI.Application;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
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
                Note = $"Giải ngân sau hoàn thành đơn (net {net:N0} {wallet.Currency}).",
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken);

        _logger.LogInformation("Released settlement for order {OrderId}, net {Net}", orderId, net);

        return true;
    }
}
