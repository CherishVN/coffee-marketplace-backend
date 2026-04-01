namespace ECommerceAPI.Application.DTOs.Admin;

public class DashboardStatsDto
{
    public UserStats Users { get; set; } = new();
    public ShopStats Shops { get; set; } = new();
    public ProductStats Products { get; set; } = new();
    public OrderStats Orders { get; set; } = new();
    public RevenueStats Revenue { get; set; } = new();
    public DisputeStats Disputes { get; set; } = new();
    /// <summary>Phí sàn tích lũy từ các đơn đã quyết toán ví.</summary>
    public PlatformFeeStats PlatformFees { get; set; } = new();
}

public class UserStats
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Suspended { get; set; }
    public int NewThisMonth { get; set; }
    public int Customers { get; set; }
    public int Sellers { get; set; }
}

public class ShopStats
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int PendingVerification { get; set; }
    public int Suspended { get; set; }
    public int NewThisMonth { get; set; }
}

public class ProductStats
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Draft { get; set; }
    public int Hidden { get; set; }
    public int OutOfStock { get; set; }
    public int NewThisMonth { get; set; }
}

public class OrderStats
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Processing { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int TodayOrders { get; set; }
    public int ThisMonthOrders { get; set; }
}

public class RevenueStats
{
    public decimal TotalRevenue { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal ThisMonthRevenue { get; set; }
    public decimal LastMonthRevenue { get; set; }
    public decimal GrowthPercentage { get; set; }
}

public class DisputeStats
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int UnderReview { get; set; }
    public int Resolved { get; set; }
    public int Refunded { get; set; }
}

public class PlatformFeeStats
{
    /// <summary>Tổng phí sàn (VND) mọi thời điểm.</summary>
    public decimal TotalFees { get; set; }
    public decimal TodayFees { get; set; }
    public decimal ThisMonthFees { get; set; }
    public decimal LastMonthFees { get; set; }
    /// <summary>Số đơn đã có bản ghi quyết toán (có phí / net).</summary>
    public int SettledOrdersCount { get; set; }
}

public class DashboardResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DashboardStatsDto? Stats { get; set; }
}

public class RecentActivityDto
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class TopShopDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class TopProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public int TotalSold { get; set; }
    public decimal Revenue { get; set; }
}
