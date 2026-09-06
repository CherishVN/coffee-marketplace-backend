using ECommerceAPI.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerceAPI.Infrastructure.Background;

/// <summary>
/// Chuyển đơn Delivered → Completed khi đủ ngày từ Đã giao và không còn khiếu nại mở (hết cửa sổ khiếu nại).
/// </summary>
public class OrderAutoCompleteBackgroundService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderAutoCompleteBackgroundService> _logger;

    public OrderAutoCompleteBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderAutoCompleteBackgroundService> logger)
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
                var orders = scope.ServiceProvider.GetRequiredService<ICustomerOrderService>();
                var n = await orders.AutoCompleteDeliveredOrdersPastDisputeWindowAsync(stoppingToken);
                if (n > 0)
                    _logger.LogInformation("[Order auto-complete] Completed {Count} order(s) after dispute window", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Order auto-complete] Sweep failed");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }
}
