using ECommerceAPI.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerceAPI.Infrastructure.Background;

/// <summary>
/// Định kỳ giải ngân ví seller khi đơn đã Hoàn thành, đủ điều kiện sau Đã giao và không khiếu nại mở.
/// </summary>
public class SellerWalletReleaseBackgroundService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SellerWalletReleaseBackgroundService> _logger;

    public SellerWalletReleaseBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SellerWalletReleaseBackgroundService> logger)
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
                var release = scope.ServiceProvider.GetRequiredService<ISellerWalletReleaseService>();
                var n = await release.ReleaseDueSettlementsAsync(stoppingToken);
                if (n > 0)
                    _logger.LogInformation("[Seller wallet] Released {Count} settlement(s) to available balance", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Seller wallet] Release sweep failed");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }
}
