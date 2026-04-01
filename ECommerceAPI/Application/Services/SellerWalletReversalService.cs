using ECommerceAPI.Application;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class SellerWalletReversalService : ISellerWalletReversalService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SellerWalletReversalService> _logger;

    public SellerWalletReversalService(
        ApplicationDbContext context,
        ILogger<SellerWalletReversalService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> TryReverseSettlementForOrderAsync(Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        var alreadyReversed = await _context.SellerWalletLedgers
            .AnyAsync(
                l => l.ReferenceType == WalletLedgerReferenceTypes.OrderRefund && l.ReferenceId == orderId,
                cancellationToken);

        if (alreadyReversed)
            return false;

        var settlement = await _context.SellerWalletLedgers
            .FirstOrDefaultAsync(
                l => l.ReferenceType == WalletLedgerReferenceTypes.OrderSettlement
                     && l.ReferenceId == orderId,
                cancellationToken);

        if (settlement == null)
            return false;

        var netToRecover = settlement.Amount;

        var wallet = await _context.SellerWallets
            .FirstOrDefaultAsync(w => w.Id == settlement.WalletId, cancellationToken);

        if (wallet == null)
        {
            _logger.LogError("Reversal: wallet {WalletId} not found for order {OrderId}", settlement.WalletId, orderId);
            return false;
        }

        if (netToRecover > 0)
        {
            wallet.AvailableBalance -= netToRecover;
            wallet.UpdatedAt = DateTime.UtcNow;
        }

        var debitLedger = new SellerWalletLedger
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Type = "debit",
            Amount = -netToRecover,
            Currency = wallet.Currency,
            ReferenceType = WalletLedgerReferenceTypes.OrderRefund,
            ReferenceId = orderId,
            Note = string.IsNullOrWhiteSpace(reason)
                ? $"Hoàn tác quyết toán đơn {orderId}"
                : $"Hoàn tác quyết toán: {reason}",
            CreatedAt = DateTime.UtcNow
        };
        await _context.SellerWalletLedgers.AddAsync(debitLedger, cancellationToken);

        var feeRow = await _context.PlatformFeeRecords
            .FirstOrDefaultAsync(r => r.OrderId == orderId && r.ReversedAt == null, cancellationToken);

        if (feeRow != null)
        {
            feeRow.ReversedAt = DateTime.UtcNow;
        }

        _logger.LogInformation(
            "Reversed wallet settlement for order {OrderId}, recovered net {Net} VND",
            orderId, netToRecover);

        return true;
    }
}
