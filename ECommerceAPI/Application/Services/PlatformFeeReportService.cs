using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class PlatformFeeReportService : IPlatformFeeReportService
{
    private readonly ApplicationDbContext _context;

    public PlatformFeeReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PlatformFeeSummaryDto> GetSummaryAsync(DateTime? fromUtc, DateTime? toUtc)
    {
        var q = ActiveFeeRecords(_context.PlatformFeeRecords.AsNoTracking());
        q = ApplyRange(q, fromUtc, toUtc);

        var agg = await q
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalFee = g.Sum(x => x.FeeAmount),
                TotalGross = g.Sum(x => x.GrossSubtotal),
                TotalNet = g.Sum(x => x.NetToSeller),
                Count = g.Count()
            })
            .FirstOrDefaultAsync();

        return new PlatformFeeSummaryDto
        {
            TotalFeeAmount = agg?.TotalFee ?? 0m,
            TotalGrossSubtotal = agg?.TotalGross ?? 0m,
            TotalNetToSeller = agg?.TotalNet ?? 0m,
            RecordCount = agg?.Count ?? 0,
            FromUtc = fromUtc,
            ToUtc = toUtc
        };
    }

    public async Task<PlatformFeeRecordsListResponseDto> GetRecordsAsync(
        int page,
        int pageSize,
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? shopId)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = ActiveFeeRecords(
            _context.PlatformFeeRecords
                .AsNoTracking()
                .Include(r => r.Shop)
                .Include(r => r.Seller));

        q = ApplyRange(q, fromUtc, toUtc);

        if (shopId.HasValue)
            q = q.Where(r => r.ShopId == shopId.Value);

        var totalCount = await q.CountAsync();

        var summary = await GetSummaryAsync(fromUtc, toUtc);
        if (shopId.HasValue)
        {
            var shopQ = ActiveFeeRecords(_context.PlatformFeeRecords.AsNoTracking())
                .Where(r => r.ShopId == shopId.Value);
            shopQ = ApplyRange(shopQ, fromUtc, toUtc);
            var shopAgg = await shopQ
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalFee = g.Sum(x => x.FeeAmount),
                    TotalGross = g.Sum(x => x.GrossSubtotal),
                    TotalNet = g.Sum(x => x.NetToSeller),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();
            summary = new PlatformFeeSummaryDto
            {
                TotalFeeAmount = shopAgg?.TotalFee ?? 0m,
                TotalGrossSubtotal = shopAgg?.TotalGross ?? 0m,
                TotalNetToSeller = shopAgg?.TotalNet ?? 0m,
                RecordCount = shopAgg?.Count ?? 0,
                FromUtc = fromUtc,
                ToUtc = toUtc
            };
        }

        var records = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new PlatformFeeRecordDto
            {
                Id = r.Id,
                OrderId = r.OrderId,
                OrderCode = r.Order.OrderCode,
                PaymentId = r.PaymentId,
                ShopId = r.ShopId,
                ShopName = r.Shop.Name,
                SellerId = r.SellerId,
                SellerName = r.Seller.FullName,
                GrossSubtotal = r.GrossSubtotal,
                CommissionPercent = r.CommissionPercent,
                FeeAmount = r.FeeAmount,
                NetToSeller = r.NetToSeller,
                Currency = r.Currency,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return new PlatformFeeRecordsListResponseDto
        {
            Success = true,
            Records = records,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Summary = summary
        };
    }

    private static IQueryable<PlatformFeeRecord> ActiveFeeRecords(IQueryable<PlatformFeeRecord> q) =>
        q.Where(r => r.ReversedAt == null);

    private static IQueryable<PlatformFeeRecord> ApplyRange(
        IQueryable<PlatformFeeRecord> q,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc.HasValue)
            q = q.Where(r => r.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue)
            q = q.Where(r => r.CreatedAt <= toUtc.Value);
        return q;
    }
}
