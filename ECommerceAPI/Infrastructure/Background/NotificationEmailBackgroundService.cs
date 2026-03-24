using ECommerceAPI.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerceAPI.Infrastructure.Background;

/// <summary>
/// Xử lý hàng đợi gửi email thông báo (không chặn request HTTP).
/// </summary>
public class NotificationEmailBackgroundService : BackgroundService
{
    private readonly INotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationEmailBackgroundService> _logger;

    public NotificationEmailBackgroundService(
        INotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationEmailBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailResolver = scope.ServiceProvider.GetRequiredService<IUserAuthEmailResolver>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var to = await emailResolver.GetEmailByUserIdAsync(job.TargetUserId, stoppingToken);
                if (string.IsNullOrWhiteSpace(to))
                {
                    _logger.LogDebug("Bỏ qua email thông báo: không lấy được email cho user {UserId}", job.TargetUserId);
                    continue;
                }

                await emailService.SendAsync(to, job.Subject, job.HtmlBody);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gửi email thông báo thất bại cho user {UserId}", job.TargetUserId);
            }
        }
    }
}
