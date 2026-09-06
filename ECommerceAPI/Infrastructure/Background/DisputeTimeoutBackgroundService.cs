using ECommerceAPI.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceAPI.Infrastructure.Background;

/// <summary>
/// Tự động xử lý khiếu nại khi shop không phản hồi trong thời hạn cấu hình (mặc định 7 ngày).
/// </summary>
public class DisputeTimeoutBackgroundService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);
    private const int TimeoutDays = 7;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DisputeTimeoutBackgroundService> _logger;

    public DisputeTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DisputeTimeoutBackgroundService> logger)
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
                var disputeService = scope.ServiceProvider.GetRequiredService<IDisputeAdminService>();

                var count = await disputeService.AutoProcessExpiredDisputesAsync(TimeoutDays, stoppingToken);
                if (count > 0)
                {
                    _logger.LogInformation(
                        "[Dispute Timeout] Tự động xử lý {Count} khiếu nại do shop không phản hồi sau {Days} ngày.",
                        count, TimeoutDays);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Dispute Timeout] Sweep job gặp lỗi");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }
}
