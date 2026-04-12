using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Seller;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class SellerWalletSettlementService : ISellerWalletSettlementService
{
    private readonly ApplicationDbContext _context;
    private readonly IPlatformFeeConfigService _platformFeeConfigService;
    private readonly ILogger<SellerWalletSettlementService> _logger;

    public SellerWalletSettlementService(
        ApplicationDbContext context,
        IPlatformFeeConfigService platformFeeConfigService,
        ILogger<SellerWalletSettlementService> logger)
    {
        _context = context;
        _platformFeeConfigService = platformFeeConfigService;
        _logger = logger;
    }

    public async Task<SellerSettlementCreditResult?> CreditSellerForPaidOrderAsync(Order order, Payment payment, CancellationToken cancellationToken = default)
    {
        if (order.Subtotal <= 0)
        {
            _logger.LogWarning(
                "Skip wallet credit: order {OrderId} has non-positive Subtotal {Subtotal}",
                order.Id, order.Subtotal);
            return null;
        }

        var alreadyCredited = await _context.SellerWalletLedgers
            .AnyAsync(
                l => l.ReferenceType == WalletLedgerReferenceTypes.OrderSettlement && l.ReferenceId == order.Id,
                cancellationToken);

        if (alreadyCredited)
            return null;

        var shop = await _context.Shops
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == order.ShopId, cancellationToken);

        if (shop == null)
        {
            _logger.LogError("Order {OrderId}: shop {ShopId} not found, cannot credit seller wallet", order.Id, order.ShopId);
            return null;
        }

        var pct = Math.Clamp(await _platformFeeConfigService.GetCurrentCommissionPercentAsync(), 0m, 100m);
        var gross = order.Subtotal;
        var fee = Math.Round(gross * pct / 100m, 2, MidpointRounding.AwayFromZero);
        if (fee > gross)
            fee = gross;
        var net = gross - fee;

        var wallet = await _context.SellerWallets
            .FirstOrDefaultAsync(w => w.SellerId == shop.OwnerId, cancellationToken);

        if (wallet == null)
        {
            wallet = new SellerWallet
            {
                Id = Guid.NewGuid(),
                SellerId = shop.OwnerId,
                AvailableBalance = 0,
                HeldBalance = 0,
                PendingBalance = 0,
                Currency = "VND",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.SellerWallets.AddAsync(wallet, cancellationToken);
            _logger.LogInformation("Created seller wallet for user {SellerId} on first settlement", shop.OwnerId);
        }

        if (net > 0)
        {
            wallet.HeldBalance += net;
            wallet.UpdatedAt = DateTime.UtcNow;
        }

        var note = pct > 0
            ? $"Tạm giữ sau thanh toán (payment {payment.Id}). Subtotal {gross:N0} VND, phí sàn {pct}% = {fee:N0} VND, net {net:N0} VND — giải ngân khi đơn hoàn thành."
            : $"Tạm giữ sau thanh toán (payment {payment.Id}). Subtotal {gross:N0} VND (không phí sàn) — giải ngân khi đơn hoàn thành.";

        var ledger = new SellerWalletLedger
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Type = "credit",
            Amount = net,
            Currency = wallet.Currency,
            ReferenceType = WalletLedgerReferenceTypes.OrderSettlement,
            ReferenceId = order.Id,
            Note = note,
            CreatedAt = DateTime.UtcNow
        };
        await _context.SellerWalletLedgers.AddAsync(ledger, cancellationToken);

        var platformFeeRecord = new PlatformFeeRecord
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PaymentId = payment.Id,
            ShopId = shop.Id,
            SellerId = shop.OwnerId,
            GrossSubtotal = gross,
            CommissionPercent = pct,
            FeeAmount = fee,
            NetToSeller = net,
            Currency = wallet.Currency,
            CreatedAt = DateTime.UtcNow
        };
        await _context.PlatformFeeRecords.AddAsync(platformFeeRecord, cancellationToken);

        _logger.LogInformation(
            "Settled order {OrderId} for seller {SellerId}: gross {Gross}, fee {Fee} ({Pct}%), net {Net}",
            order.Id, shop.OwnerId, gross, fee, pct, net);

        return new SellerSettlementCreditResult
        {
            SellerId = shop.OwnerId,
            NetAmount = net,
            GrossSubtotal = gross,
            PlatformFeeAmount = fee,
            CommissionPercent = pct
        };
    }
}
