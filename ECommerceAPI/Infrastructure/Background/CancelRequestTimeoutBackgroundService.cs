using ECommerceAPI.Application.Interfaces;

namespace ECommerceAPI.Infrastructure.Background;

/// <summary>
/// Tự động hủy yêu cầu hủy đơn hàng khi shop không phản hồi trong thời hạn cấu hình (mặc định 24h).
/// </summary>
public class CancelRequestTimeoutBackgroundService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CancelRequestTimeoutBackgroundService> _logger;

    public CancelRequestTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CancelRequestTimeoutBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<ICustomerOrderService>();

                var count = await orderService.AutoCancelExpiredCancelRequestsAsync(stoppingToken);
                if (count > 0)
                {
                    _logger.LogInformation(
                        "[CancelRequest Timeout] Tự động hủy {Count} đơn hàng do shop không phản hồi yêu cầu hủy.",
                        count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CancelRequest Timeout] Sweep job gặp lỗi");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }
}
