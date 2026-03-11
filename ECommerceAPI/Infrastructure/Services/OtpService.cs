using System.Collections.Concurrent;
using ECommerceAPI.Application.Interfaces;

namespace ECommerceAPI.Infrastructure.Services;

public class OtpService : IOtpService
{
    private record OtpEntry(string Code, DateTime Expiry);

    private readonly ConcurrentDictionary<string, OtpEntry> _store = new();

    public string GenerateAndStore(Guid userId, string newEmail)
    {
        var expired = _store.Where(kv => kv.Value.Expiry < DateTime.UtcNow)
                            .Select(kv => kv.Key).ToList();
        foreach (var k in expired) _store.TryRemove(k, out _);

        var code = Random.Shared.Next(100000, 999999).ToString();
        var key = MakeKey(userId, newEmail);
        _store[key] = new OtpEntry(code, DateTime.UtcNow.AddMinutes(10));
        return code;
    }

    public bool Verify(Guid userId, string newEmail, string otp)
    {
        var key = MakeKey(userId, newEmail);
        if (!_store.TryGetValue(key, out var entry)) return false;

        if (entry.Expiry < DateTime.UtcNow)
        {
            _store.TryRemove(key, out _);
            return false;
        }

        if (entry.Code != otp) return false;

        _store.TryRemove(key, out _);
        return true;
    }

    private static string MakeKey(Guid userId, string email) =>
        $"{userId}:{email.Trim().ToLowerInvariant()}";
}
