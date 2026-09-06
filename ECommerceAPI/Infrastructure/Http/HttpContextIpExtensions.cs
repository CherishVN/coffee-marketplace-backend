using Microsoft.AspNetCore.Http;

namespace ECommerceAPI.Infrastructure.Http;

public static class HttpContextIpExtensions
{
    /// <summary>
    /// IP thật của client sau proxy (Cloud Run / Load Balancer gửi X-Forwarded-For).
    /// </summary>
    public static string GetClientIpAddress(this HttpContext context)
    {
        var fwd = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd))
        {
            var ip = fwd.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
                return StripIPv4MappedPrefix(ip);
        }

        var remote = context.Connection.RemoteIpAddress;
        if (remote != null)
        {
            if (remote.IsIPv4MappedToIPv6)
                remote = remote.MapToIPv4();
            return remote.ToString();
        }

        return "127.0.0.1";
    }

    private static string StripIPv4MappedPrefix(string ip)
    {
        const string p = "::ffff:";
        return ip.StartsWith(p, StringComparison.OrdinalIgnoreCase) ? ip[p.Length..] : ip;
    }
}
