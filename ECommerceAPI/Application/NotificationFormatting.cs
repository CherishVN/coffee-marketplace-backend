namespace ECommerceAPI.Application;

internal static class NotificationFormatting
{
    public static string ShortEntityId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();
}
