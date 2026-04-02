using System.Text.Json;
using System.Text;
using ECommerceAPI.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECommerceAPI.Infrastructure.Services;

public class SupabaseAuthEmailResolver : IUserAuthEmailResolver
{
    private sealed record AuthUserCacheItem(string? Email, string? AvatarUrl);

    private static readonly TimeSpan SuccessTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan EmptyTtl = TimeSpan.FromMinutes(2);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SupabaseAuthEmailResolver> _logger;

    public SupabaseAuthEmailResolver(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ILogger<SupabaseAuthEmailResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<string?> GetEmailByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var authUser = await GetAuthUserAsync(userId, cancellationToken);
        return authUser.Email;
    }

    public async Task<string?> GetAvatarUrlByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var authUser = await GetAuthUserAsync(userId, cancellationToken);
        return authUser.AvatarUrl;
    }

    private async Task<(string? Email, string? AvatarUrl)> GetAuthUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cacheKey = $"supabase-auth-user:{userId}";
        if (_memoryCache.TryGetValue<AuthUserCacheItem>(cacheKey, out var cached)
            && cached is not null)
        {
            return (cached.Email, cached.AvatarUrl);
        }

        var supabaseUrl = _configuration["Supabase:Url"];
        var serviceRoleKey = _configuration["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return CacheAndReturnEmpty(cacheKey);

        using var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Add("apikey", serviceRoleKey);
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceRoleKey}");

        var response = await http.GetAsync(
            $"{supabaseUrl.TrimEnd('/')}/auth/v1/admin/users/{userId}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug(
                "Supabase admin user lookup failed for {UserId}. Status: {Status}",
                userId,
                response.StatusCode);
            return CacheAndReturnEmpty(cacheKey);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var root = doc.RootElement;
        if (root.TryGetProperty("user", out var userElement) && userElement.ValueKind == JsonValueKind.Object)
        {
            root = userElement;
        }

        string? email = null;
        if (root.TryGetProperty("email", out var emailElement) && emailElement.ValueKind == JsonValueKind.String)
        {
            email = emailElement.GetString();
        }

        string? avatarUrl = null;
        if (root.TryGetProperty("user_metadata", out var metadataElement) && metadataElement.ValueKind == JsonValueKind.Object)
        {
            if (metadataElement.TryGetProperty("avatar_storage_path", out var storagePathElement)
                && storagePathElement.ValueKind == JsonValueKind.String)
            {
                var storagePath = storagePathElement.GetString();
                if (!string.IsNullOrWhiteSpace(storagePath))
                {
                    avatarUrl = await CreateSignedAvatarUrlAsync(
                        http,
                        supabaseUrl,
                        storagePath,
                        cancellationToken);
                }
            }

            if (metadataElement.TryGetProperty("avatar_url", out var avatarElement)
                && avatarElement.ValueKind == JsonValueKind.String
                && string.IsNullOrWhiteSpace(avatarUrl))
            {
                avatarUrl = avatarElement.GetString();
            }
        }

        var item = new AuthUserCacheItem(email, avatarUrl);
        var ttl = string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(avatarUrl)
            ? EmptyTtl
            : SuccessTtl;

        _memoryCache.Set(cacheKey, item, ttl);
        return (item.Email, item.AvatarUrl);
    }

    private (string? Email, string? AvatarUrl) CacheAndReturnEmpty(string cacheKey)
    {
        var empty = new AuthUserCacheItem(null, null);
        _memoryCache.Set(cacheKey, empty, EmptyTtl);
        return (empty.Email, empty.AvatarUrl);
    }

    private async Task<string?> CreateSignedAvatarUrlAsync(
        HttpClient http,
        string supabaseUrl,
        string storagePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var encodedPath = Uri.EscapeDataString(storagePath).Replace("%2F", "/");
            using var body = new StringContent(
                "{\"expiresIn\":3600}",
                Encoding.UTF8,
                "application/json");

            var response = await http.PostAsync(
                $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/sign/avatars/{encodedPath}",
                body,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(payload);

            if (doc.RootElement.TryGetProperty("signedURL", out var signedUrlElement)
                && signedUrlElement.ValueKind == JsonValueKind.String)
            {
                var signedUrl = signedUrlElement.GetString();
                if (!string.IsNullOrWhiteSpace(signedUrl))
                {
                    if (Uri.TryCreate(signedUrl, UriKind.Absolute, out _))
                        return signedUrl;

                    return $"{supabaseUrl.TrimEnd('/')}/storage/v1{signedUrl}";
                }
            }
        }
        catch
        {
            // fallback to avatar_url from metadata
        }

        return null;
    }
}
