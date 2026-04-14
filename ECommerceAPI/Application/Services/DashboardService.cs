using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        ApplicationDbContext context,
        ILogger<DashboardService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DashboardResponseDto> GetDashboardStatsAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfToday = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
            var lastMonthStart = startOfMonth.AddMonths(-1);

            // Batch all counts per entity into single queries to avoid
            // multiple round-trips AND DbContext concurrency issues.

            // 1) User Stats — single query
            var roleCodes = await _context.Roles
                .ToDictionaryAsync(r => r.Code, r => r.Id);
            var customerRoleId = roleCodes.GetValueOrDefault("customer");
            var sellerRoleId = roleCodes.GetValueOrDefault("seller");

            var userStats = await _context.Users
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Active = g.Count(u => u.Status == (short)UserStatus.Active),
                    Suspended = g.Count(u => u.Status == (short)UserStatus.Suspended),
                    NewThisMonth = g.Count(u => u.CreatedAt >= startOfMonth),
                    Customers = g.Count(u => u.RoleId == customerRoleId),
                    Sellers = g.Count(u => u.RoleId == sellerRoleId),
                })
                .FirstOrDefaultAsync();

            // 2) Shop Stats — single query (bỏ shop bị từ chối duyệt: VerificationStatus = Rejected)
            var shopStats = await _context.Shops
                .Where(s => s.VerificationStatus != (short)ShopVerificationStatus.Rejected)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Active = g.Count(s =>
                        s.Status == (short)ShopStatus.Active &&
                        s.VerificationStatus == (short)ShopVerificationStatus.Verified),
                    PendingVerification = g.Count(s => s.VerificationStatus == (short)ShopVerificationStatus.Pending),
                    Suspended = g.Count(s => s.Status == (short)ShopStatus.Suspended),
                    NewThisMonth = g.Count(s => s.CreatedAt >= startOfMonth),
                })
                .FirstOrDefaultAsync();

            // 3) Product Stats — single query
            var productStats = await _context.Products
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Active = g.Count(p => p.Status == (short)ProductStatus.Active),
                    Draft = g.Count(p => p.Status == (short)ProductStatus.Draft),
                    Hidden = g.Count(p => p.Status == (short)ProductStatus.Hidden),
                    OutOfStock = g.Count(p => p.Status == (short)ProductStatus.OutOfStock),
                    NewThisMonth = g.Count(p => p.CreatedAt >= startOfMonth),
                })
                .FirstOrDefaultAsync();

            // 4) Order Stats — single query
            var orderStats = await _context.Orders
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Pending = g.Count(o =>
                        o.Status == (short)OrderStatus.PendingPayment ||
                        o.Status == (short)OrderStatus.PendingConfirmation),
                    Processing = g.Count(o =>
                        o.Status == (short)OrderStatus.Processing ||
                        o.Status == (short)OrderStatus.Shipping),
                    Confirmed = g.Count(o => o.Status == (short)OrderStatus.Confirmed),
                    Delivered = g.Count(o => o.Status == (short)OrderStatus.Delivered),
                    Completed = g.Count(o => o.Status == (short)OrderStatus.Completed),
                    Cancelled = g.Count(o => o.Status == (short)OrderStatus.Cancelled),
                    Refunded = g.Count(o => o.Status == (short)OrderStatus.Refunded),
                    TodayOrders = g.Count(o => o.CreatedAt >= startOfToday),
                    ThisMonthOrders = g.Count(o => o.CreatedAt >= startOfMonth),
                })
                .FirstOrDefaultAsync();

            // 5) Revenue Stats — single query
            var revenueStats = await _context.Orders
                .Where(o => o.Status == (short)OrderStatus.Completed)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalRevenue = g.Sum(o => (decimal?)o.Total) ?? 0m,
                    TodayRevenue = g.Where(o => o.CreatedAt >= startOfToday).Sum(o => (decimal?)o.Total) ?? 0m,
                    ThisMonthRevenue = g.Where(o => o.CreatedAt >= startOfMonth).Sum(o => (decimal?)o.Total) ?? 0m,
                    LastMonthRevenue = g.Where(o => o.CreatedAt >= lastMonthStart && o.CreatedAt < startOfMonth).Sum(o => (decimal?)o.Total) ?? 0m,
                })
                .FirstOrDefaultAsync();

            var totalRevenue = revenueStats?.TotalRevenue ?? 0m;
            var todayRevenue = revenueStats?.TodayRevenue ?? 0m;
            var thisMonthRevenue = revenueStats?.ThisMonthRevenue ?? 0m;
            var lastMonthRevenue = revenueStats?.LastMonthRevenue ?? 0m;

            var growthPercentage = lastMonthRevenue > 0
                ? ((thisMonthRevenue - lastMonthRevenue) / lastMonthRevenue) * 100
                : 0m;

            // 5b) Platform fee (phí sàn) — từ bảng platform_fee_records
            var platformFeeStats = await _context.PlatformFeeRecords
                .AsNoTracking()
                .Where(r => r.ReversedAt == null)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Sum(x => x.FeeAmount),
                    Today = g.Where(x => x.CreatedAt >= startOfToday).Sum(x => x.FeeAmount),
                    ThisMonth = g.Where(x => x.CreatedAt >= startOfMonth).Sum(x => x.FeeAmount),
                    LastMonth = g.Where(x => x.CreatedAt >= lastMonthStart && x.CreatedAt < startOfMonth).Sum(x => x.FeeAmount),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            // 6) Dispute Stats — single query
            var disputeStats = await _context.Disputes
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Pending = g.Count(d => d.Status == (short)DisputeStatus.Pending),
                    UnderReview = g.Count(d => d.Status == (short)DisputeStatus.UnderReview),
                    Resolved = g.Count(d => d.Status == (short)DisputeStatus.Resolved),
                    Refunded = g.Count(d => d.Status == (short)DisputeStatus.Refunded),
                })
                .FirstOrDefaultAsync();

            var stats = new DashboardStatsDto
            {
                Users = new UserStats
                {
                    Total = userStats?.Total ?? 0,
                    Active = userStats?.Active ?? 0,
                    Suspended = userStats?.Suspended ?? 0,
                    NewThisMonth = userStats?.NewThisMonth ?? 0,
                    Customers = userStats?.Customers ?? 0,
                    Sellers = userStats?.Sellers ?? 0
                },
                Shops = new ShopStats
                {
                    Total = shopStats?.Total ?? 0,
                    Active = shopStats?.Active ?? 0,
                    PendingVerification = shopStats?.PendingVerification ?? 0,
                    Suspended = shopStats?.Suspended ?? 0,
                    NewThisMonth = shopStats?.NewThisMonth ?? 0
                },
                Products = new ProductStats
                {
                    Total = productStats?.Total ?? 0,
                    Active = productStats?.Active ?? 0,
                    Draft = productStats?.Draft ?? 0,
                    Hidden = productStats?.Hidden ?? 0,
                    OutOfStock = productStats?.OutOfStock ?? 0,
                    NewThisMonth = productStats?.NewThisMonth ?? 0
                },
                Orders = new OrderStats
                {
                    Total = orderStats?.Total ?? 0,
                    Pending = orderStats?.Pending ?? 0,
                    Processing = orderStats?.Processing ?? 0,
                    Confirmed = orderStats?.Confirmed ?? 0,
                    Delivered = orderStats?.Delivered ?? 0,
                    Completed = orderStats?.Completed ?? 0,
                    Cancelled = orderStats?.Cancelled ?? 0,
                    Refunded = orderStats?.Refunded ?? 0,
                    TodayOrders = orderStats?.TodayOrders ?? 0,
                    ThisMonthOrders = orderStats?.ThisMonthOrders ?? 0
                },
                Revenue = new RevenueStats
                {
                    TotalRevenue = totalRevenue,
                    TodayRevenue = todayRevenue,
                    ThisMonthRevenue = thisMonthRevenue,
                    LastMonthRevenue = lastMonthRevenue,
                    GrowthPercentage = growthPercentage
                },
                Disputes = new DisputeStats
                {
                    Total = disputeStats?.Total ?? 0,
                    Pending = disputeStats?.Pending ?? 0,
                    UnderReview = disputeStats?.UnderReview ?? 0,
                    Resolved = disputeStats?.Resolved ?? 0,
                    Refunded = disputeStats?.Refunded ?? 0
                },
                PlatformFees = new PlatformFeeStats
                {
                    TotalFees = platformFeeStats?.Total ?? 0m,
                    TodayFees = platformFeeStats?.Today ?? 0m,
                    ThisMonthFees = platformFeeStats?.ThisMonth ?? 0m,
                    LastMonthFees = platformFeeStats?.LastMonth ?? 0m,
                    SettledOrdersCount = platformFeeStats?.Count ?? 0
                }
            };

            return new DashboardResponseDto
            {
                Success = true,
                Stats = stats
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            return new DashboardResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy thống kê"
            };
        }
    }

    public async Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int limit = 10)
    {
        var activities = new List<RecentActivityDto>();

        try
        {
            // Recent orders
            var recentOrders = await _context.Orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(limit)
                .Select(o => new RecentActivityDto
                {
                    Type = "Order",
                    Description = $"Đơn hàng mới từ {o.Customer.FullName}",
                    Timestamp = o.CreatedAt
                })
                .ToListAsync();

            activities.AddRange(recentOrders);

            // Recent shops
            var recentShops = await _context.Shops
                .OrderByDescending(s => s.CreatedAt)
                .Take(5)
                .Select(s => new RecentActivityDto
                {
                    Type = "Shop",
                    Description = $"Shop mới đăng ký: {s.Name}",
                    Timestamp = s.CreatedAt
                })
                .ToListAsync();

            activities.AddRange(recentShops);

            return activities
                .OrderByDescending(a => a.Timestamp)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent activities");
            return activities;
        }
    }

    public async Task<List<TopShopDto>> GetTopShopsAsync(int limit = 10)
    {
        try
        {
            var topShops = await _context.Shops
                .Select(s => new
                {
                    Shop = s,
                    TotalOrders = s.Orders.Count(o => o.Status == (short)OrderStatus.Completed),
                    TotalRevenue = s.Orders
                        .Where(o => o.Status == (short)OrderStatus.Completed)
                        .Sum(o => o.Total)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(limit)
                .Select(x => new TopShopDto
                {
                    Id = x.Shop.Id,
                    Name = x.Shop.Name,
                    TotalOrders = x.TotalOrders,
                    TotalRevenue = x.TotalRevenue
                })
                .ToListAsync();

            return topShops;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top shops");
            return new List<TopShopDto>();
        }
    }

    public async Task<List<TopProductDto>> GetTopProductsAsync(int limit = 10)
    {
        try
        {
            var topProducts = await _context.Products
                .Include(p => p.Shop)
                .Include(p => p.OrderItems)
                .Select(p => new
                {
                    Product = p,
                    TotalSold = p.OrderItems
                        .Where(oi => oi.Order.Status == (short)OrderStatus.Completed)
                        .Sum(oi => (int?)oi.Quantity) ?? 0,
                    Revenue = p.OrderItems
                        .Where(oi => oi.Order.Status == (short)OrderStatus.Completed)
                        .Sum(oi => (decimal?)oi.LineTotal) ?? 0
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(limit)
                .Select(x => new TopProductDto
                {
                    Id = x.Product.Id,
                    Name = x.Product.Name,
                    ShopName = x.Product.Shop.Name,
                    TotalSold = x.TotalSold,
                    Revenue = x.Revenue
                })
                .ToListAsync();

            return topProducts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top products");
            return new List<TopProductDto>();
        }
    }
}
