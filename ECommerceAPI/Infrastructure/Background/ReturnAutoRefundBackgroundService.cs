using ECommerceAPI.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerceAPI.Infrastructure.Background;

/// <summary>
/// Tu dong hoan tien khi da nhan hang tra du 7 ngay.
/// </summary>
public class ReturnAutoRefundBackgroundService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);
    private const int DefaultDays = 7;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReturnAutoRefundBackgroundService> _logger;

    public ReturnAutoRefundBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReturnAutoRefundBackgroundService> logger)
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
                var disputes = scope.ServiceProvider.GetRequiredService<IDisputeAdminService>();
                var n = await disputes.AutoRefundReturnedDisputesAsync(DefaultDays, stoppingToken);
                if (n > 0)
                    _logger.LogInformation("[Return auto-refund] Refunded {Count} dispute(s) after {Days} days", n, DefaultDays);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Return auto-refund] Sweep failed");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }
}
