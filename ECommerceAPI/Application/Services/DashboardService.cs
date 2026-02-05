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
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfToday = now.Date;

            // User Stats
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.Status == (short)UserStatus.Active);
            var suspendedUsers = await _context.Users.CountAsync(u => u.Status == (short)UserStatus.Suspended);
            var newUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= startOfMonth);
            var customers = await _context.Users.CountAsync(u => u.Role == "customer");
            var sellers = await _context.Users.CountAsync(u => u.Role == "seller");

            // Shop Stats
            var totalShops = await _context.Shops.CountAsync();
            var activeShops = await _context.Shops.CountAsync(s => s.Status == (short)ShopStatus.Active);
            var pendingShops = await _context.Shops.CountAsync(s => s.VerificationStatus == (short)ShopVerificationStatus.Pending);
            var suspendedShops = await _context.Shops.CountAsync(s => s.Status == (short)ShopStatus.Suspended);
            var newShopsThisMonth = await _context.Shops.CountAsync(s => s.CreatedAt >= startOfMonth);

            // Product Stats
            var totalProducts = await _context.Products.CountAsync();
            var activeProducts = await _context.Products.CountAsync(p => p.Status == (short)ProductStatus.Active);
            var draftProducts = await _context.Products.CountAsync(p => p.Status == (short)ProductStatus.Draft);
            var hiddenProducts = await _context.Products.CountAsync(p => p.Status == (short)ProductStatus.Hidden);
            var outOfStockProducts = await _context.Products.CountAsync(p => p.Status == (short)ProductStatus.OutOfStock);
            var newProductsThisMonth = await _context.Products.CountAsync(p => p.CreatedAt >= startOfMonth);

            // Order Stats
            var totalOrders = await _context.Orders.CountAsync();
            var pendingOrders = await _context.Orders.CountAsync(o => 
                o.Status == (short)OrderStatus.PendingPayment || 
                o.Status == (short)OrderStatus.PendingConfirmation);
            var processingOrders = await _context.Orders.CountAsync(o => 
                o.Status == (short)OrderStatus.Processing || 
                o.Status == (short)OrderStatus.Shipping);
            var completedOrders = await _context.Orders.CountAsync(o => o.Status == (short)OrderStatus.Completed);
            var cancelledOrders = await _context.Orders.CountAsync(o => o.Status == (short)OrderStatus.Cancelled);
            var todayOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= startOfToday);
            var thisMonthOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= startOfMonth);

            // Revenue Stats
            var totalRevenue = await _context.Orders
                .Where(o => o.Status == (short)OrderStatus.Completed || o.Status == (short)OrderStatus.Delivered)
                .SumAsync(o => o.Total);
                
            var todayRevenue = await _context.Orders
                .Where(o => o.CreatedAt >= startOfToday && 
                           (o.Status == (short)OrderStatus.Completed || o.Status == (short)OrderStatus.Delivered))
                .SumAsync(o => o.Total);
                
            var thisMonthRevenue = await _context.Orders
                .Where(o => o.CreatedAt >= startOfMonth && 
                           (o.Status == (short)OrderStatus.Completed || o.Status == (short)OrderStatus.Delivered))
                .SumAsync(o => o.Total);

            var lastMonthStart = startOfMonth.AddMonths(-1);
            var lastMonthRevenue = await _context.Orders
                .Where(o => o.CreatedAt >= lastMonthStart && o.CreatedAt < startOfMonth &&
                           (o.Status == (short)OrderStatus.Completed || o.Status == (short)OrderStatus.Delivered))
                .SumAsync(o => o.Total);

            var growthPercentage = lastMonthRevenue > 0 
                ? ((thisMonthRevenue - lastMonthRevenue) / lastMonthRevenue) * 100 
                : 0;

            // Dispute Stats
            var totalDisputes = await _context.Disputes.CountAsync();
            var pendingDisputes = await _context.Disputes.CountAsync(d => d.Status == (short)DisputeStatus.Pending);
            var underReviewDisputes = await _context.Disputes.CountAsync(d => d.Status == (short)DisputeStatus.UnderReview);
            var resolvedDisputes = await _context.Disputes.CountAsync(d => d.Status == (short)DisputeStatus.Resolved);
            var refundedDisputes = await _context.Disputes.CountAsync(d => d.Status == (short)DisputeStatus.Refunded);

            var stats = new DashboardStatsDto
            {
                Users = new UserStats
                {
                    Total = totalUsers,
                    Active = activeUsers,
                    Suspended = suspendedUsers,
                    NewThisMonth = newUsersThisMonth,
                    Customers = customers,
                    Sellers = sellers
                },
                Shops = new ShopStats
                {
                    Total = totalShops,
                    Active = activeShops,
                    PendingVerification = pendingShops,
                    Suspended = suspendedShops,
                    NewThisMonth = newShopsThisMonth
                },
                Products = new ProductStats
                {
                    Total = totalProducts,
                    Active = activeProducts,
                    Draft = draftProducts,
                    Hidden = hiddenProducts,
                    OutOfStock = outOfStockProducts,
                    NewThisMonth = newProductsThisMonth
                },
                Orders = new OrderStats
                {
                    Total = totalOrders,
                    Pending = pendingOrders,
                    Processing = processingOrders,
                    Completed = completedOrders,
                    Cancelled = cancelledOrders,
                    TodayOrders = todayOrders,
                    ThisMonthOrders = thisMonthOrders
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
                    Total = totalDisputes,
                    Pending = pendingDisputes,
                    UnderReview = underReviewDisputes,
                    Resolved = resolvedDisputes,
                    Refunded = refundedDisputes
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
                    TotalSold = p.OrderItems.Sum(oi => oi.Quantity),
                    Revenue = p.OrderItems.Sum(oi => oi.LineTotal)
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
