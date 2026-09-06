using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardResponseDto> GetDashboardStatsAsync();
    Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int limit = 10);
    Task<List<TopShopDto>> GetTopShopsAsync(int limit = 10);
    Task<List<TopProductDto>> GetTopProductsAsync(int limit = 10);
}
