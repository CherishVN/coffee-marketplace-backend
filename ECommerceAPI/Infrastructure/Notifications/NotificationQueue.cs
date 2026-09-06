using System.Threading.Channels;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Application.Notifications;

namespace ECommerceAPI.Infrastructure.Notifications;

public sealed class NotificationQueue : INotificationQueue
{
    private readonly Channel<NotificationEmailJob> _channel = Channel.CreateUnbounded<NotificationEmailJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ChannelReader<NotificationEmailJob> Reader => _channel.Reader;

    public ValueTask EnqueueEmailAsync(NotificationEmailJob job, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(job, cancellationToken);
}
