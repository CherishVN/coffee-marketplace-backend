using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerceAI.Middleware;

/// <summary>
/// Enriches the JWT claims with the actual user role.
/// Instead of querying the AI DB, it calls the Main API's internal endpoint
/// and caches the result in memory for 10 minutes.
/// This avoids the need to replicate the users table in the AI database.
/// </summary>
public class UserRoleSyncMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserRoleSyncMiddleware> _logger;

    public UserRoleSyncMiddleware(RequestDelegate next, ILogger<UserRoleSyncMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IMemoryCache cache, IHttpClientFactory httpClientFactory)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirstValue("sub")
                   ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (Guid.TryParse(sub, out var userId))
            {
                try
                {
                    // Cache key theo từng user, TTL 10 phút
                    var cacheKey = $"user_role_{userId}";

                    if (!cache.TryGetValue(cacheKey, out string? roleCode))
                    {
                        // Gọi HTTP sang Main API để lấy role
                        var client = httpClientFactory.CreateClient("MainApi");
                        var response = await client.GetAsync($"/api/internal/users/{userId}/role");

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            roleCode = doc.RootElement.GetProperty("roleCode").GetString();

                            // Cache kết quả 10 phút
                            cache.Set(cacheKey, roleCode, TimeSpan.FromMinutes(10));
                            _logger.LogDebug("Role fetched from MainAPI for user {UserId}: {Role}", userId, roleCode);
                        }
                        else
                        {
                            _logger.LogWarning("MainAPI returned {Status} for user {UserId} role lookup", response.StatusCode, userId);
                        }
                    }

                    if (!string.IsNullOrEmpty(roleCode) && context.User.Identity is ClaimsIdentity identity)
                    {
                        var existing = identity.FindFirst(ClaimTypes.Role);
                        if (existing != null) identity.RemoveClaim(existing);
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleCode));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "UserRoleSyncMiddleware: error looking up role for user {UserId}", userId);
                }
            }
        }

        await _next(context);
    }
}
