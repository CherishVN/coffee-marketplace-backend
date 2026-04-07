using ECommerceAPI.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerceAPI.Infrastructure.Background;

/// <summary>
/// Tự động hết hạn đơn chờ thanh toán để tránh kẹt đơn khi người dùng bỏ ngang ở cổng thanh toán.
/// </summary>
public class PaymentTimeoutBackgroundService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentTimeoutBackgroundService> _logger;

    public PaymentTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentTimeoutBackgroundService> logger)
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
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

                var expiredCount = await paymentService.ExpireStalePendingPaymentsAsync(stoppingToken);
                if (expiredCount > 0)
                {
                    _logger.LogInformation("[Payment Timeout] Auto-cancelled {Count} stale pending order(s)", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Payment Timeout] Sweep job failed");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }
}
