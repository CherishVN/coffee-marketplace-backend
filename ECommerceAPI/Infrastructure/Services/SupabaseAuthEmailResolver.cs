using System.Globalization;
using System.Text;
using System.Text.Json;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECommerceAPI.Infrastructure.Services;

public class SupabaseAuthEmailResolver : IUserAuthEmailResolver
{
    private static readonly object FailedSentinel = new();

    private sealed class AuthSnapshot
    {
        public string? Email { get; init; }
        public string? AvatarUrl { get; init; }
        public DateTime? LastSignInAt { get; init; }
        public DateTime? AuthUserCreatedAt { get; init; }
        public DateTime? EmailConfirmedAt { get; init; }
        public string? AuthPhone { get; init; }
        public string? AuthDisplayName { get; init; }
        public List<string> Providers { get; init; } = new();
    }

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
        var s = await LoadSnapshotAsync(userId, cancellationToken);
        return s?.Email;
    }

    public async Task<string?> GetAvatarUrlByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var s = await LoadSnapshotAsync(userId, cancellationToken);
        return s?.AvatarUrl;
    }

    public async Task<SupabaseAuthEnrichmentDto?> GetSupabaseAuthEnrichmentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var s = await LoadSnapshotAsync(userId, cancellationToken);
        if (s is null)
            return null;

        return new SupabaseAuthEnrichmentDto
        {
            Email = s.Email,
            AvatarUrl = s.AvatarUrl,
            Details = new SupabaseAuthInfoDto
            {
                LastSignInAt = s.LastSignInAt,
                AuthUserCreatedAt = s.AuthUserCreatedAt,
                EmailConfirmedAt = s.EmailConfirmedAt,
                AuthPhone = s.AuthPhone,
                AuthDisplayName = s.AuthDisplayName,
                Providers = s.Providers.ToList(),
            },
        };
    }

    private async Task<AuthSnapshot?> LoadSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cacheKey = $"supabase-auth-user:{userId}";
        if (_memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is not null)
        {
            if (ReferenceEquals(cachedObj, FailedSentinel))
                return null;
            if (cachedObj is AuthSnapshot cachedSnap)
                return cachedSnap;
        }

        var supabaseUrl = _configuration["Supabase:Url"];
        var serviceRoleKey = _configuration["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            _memoryCache.Set(cacheKey, FailedSentinel, EmptyTtl);
            return null;
        }

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
            _memoryCache.Set(cacheKey, FailedSentinel, EmptyTtl);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        var root = doc.RootElement;
        if (root.TryGetProperty("user", out var userElement) && userElement.ValueKind == JsonValueKind.Object)
            root = userElement;

        string? email = null;
        if (root.TryGetProperty("email", out var emailEl) && emailEl.ValueKind == JsonValueKind.String)
            email = emailEl.GetString();

        string? phone = null;
        if (root.TryGetProperty("phone", out var phoneEl) && phoneEl.ValueKind == JsonValueKind.String)
            phone = phoneEl.GetString();

        var lastSignIn = ParseIsoDate(root, "last_sign_in_at");
        var createdAt = ParseIsoDate(root, "created_at");
        var emailConfirmed = ParseIsoDate(root, "email_confirmed_at");

        var providers = new List<string>();
        if (root.TryGetProperty("identities", out var idents) && idents.ValueKind == JsonValueKind.Array)
        {
            foreach (var id in idents.EnumerateArray())
            {
                if (id.TryGetProperty("provider", out var p) && p.ValueKind == JsonValueKind.String)
                {
                    var pv = p.GetString();
                    if (!string.IsNullOrWhiteSpace(pv) && !providers.Contains(pv, StringComparer.OrdinalIgnoreCase))
                        providers.Add(pv);
                }
            }
        }

        string? displayName = null;
        if (root.TryGetProperty("user_metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            displayName = FirstString(meta, "full_name", "name", "display_name", "fullName");
        }

        string? avatarUrl = null;
        if (root.TryGetProperty("user_metadata", out var metadataElement) && metadataElement.ValueKind == JsonValueKind.Object)
        {
            var avatarBucket = _configuration["Supabase:AvatarBucket"] ?? "image";
            if (metadataElement.TryGetProperty("avatar_storage_bucket", out var bucketElement)
                && bucketElement.ValueKind == JsonValueKind.String)
            {
                var metadataBucket = bucketElement.GetString();
                if (!string.IsNullOrWhiteSpace(metadataBucket))
                    avatarBucket = metadataBucket;
            }

            if (metadataElement.TryGetProperty("avatar_storage_path", out var storagePathElement)
                && storagePathElement.ValueKind == JsonValueKind.String)
            {
                var storagePath = storagePathElement.GetString();
                if (!string.IsNullOrWhiteSpace(storagePath))
                {
                    avatarUrl = await CreateSignedAvatarUrlAsync(
                        http,
                        supabaseUrl,
                        avatarBucket,
                        storagePath,
                        cancellationToken);
                }
            }
        }

        var snapshot = new AuthSnapshot
        {
            Email = email,
            AvatarUrl = avatarUrl,
            LastSignInAt = lastSignIn,
            AuthUserCreatedAt = createdAt,
            EmailConfirmedAt = emailConfirmed,
            AuthPhone = phone,
            AuthDisplayName = displayName,
            Providers = providers,
        };

        var ttl = string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(avatarUrl) && providers.Count == 0
            ? EmptyTtl
            : SuccessTtl;
        _memoryCache.Set(cacheKey, snapshot, ttl);
        return snapshot;
    }

    private static DateTime? ParseIsoDate(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;
        if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined)
            return null;
        if (el.ValueKind != JsonValueKind.String)
            return null;
        var s = el.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return null;
        if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return null;
        return dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : dt.ToUniversalTime();
    }

    private static string? FirstString(JsonElement obj, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (obj.TryGetProperty(k, out var el) && el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
        }

        return null;
    }

    private async Task<string?> CreateSignedAvatarUrlAsync(
        HttpClient http,
        string supabaseUrl,
        string bucket,
        string storagePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalizedBucket = bucket.Trim().Trim('/');
            var normalizedPath = storagePath.Trim().Trim('/');

            if (normalizedPath.StartsWith($"{normalizedBucket}/", StringComparison.OrdinalIgnoreCase))
                normalizedPath = normalizedPath[(normalizedBucket.Length + 1)..];

            var encodedPath = Uri.EscapeDataString(normalizedPath).Replace("%2F", "/");
            using var body = new StringContent(
                "{\"expiresIn\":3600}",
                Encoding.UTF8,
                "application/json");

            var response = await http.PostAsync(
                $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/sign/{normalizedBucket}/{encodedPath}",
                body,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

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
            // Ignore
        }

        return null;
    }
}
