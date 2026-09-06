using System.Threading.Channels;
using ECommerceAPI.Application.Notifications;

namespace ECommerceAPI.Application.Interfaces;

public interface INotificationQueue
{
    ChannelReader<NotificationEmailJob> Reader { get; }

    ValueTask EnqueueEmailAsync(NotificationEmailJob job, CancellationToken cancellationToken = default);
}
