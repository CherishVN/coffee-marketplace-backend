using System.Text.Json;
using ECommerceAPI.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECommerceAPI.Infrastructure.Services;

public class SupabaseAuthEmailResolver : IUserAuthEmailResolver
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SupabaseAuthEmailResolver> _logger;

    public SupabaseAuthEmailResolver(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SupabaseAuthEmailResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> GetEmailByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var supabaseUrl = _configuration["Supabase:Url"];
        var serviceRoleKey = _configuration["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return null;

        using var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Add("apikey", serviceRoleKey);
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceRoleKey}");

        var response = await http.GetAsync(
            $"{supabaseUrl.TrimEnd('/')}/auth/v1/admin/users/{userId}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase admin user lookup failed for {UserId}. Status: {Status}",
                userId,
                response.StatusCode);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("email", out var rootEmailElement)
            && rootEmailElement.ValueKind == JsonValueKind.String)
        {
            return rootEmailElement.GetString();
        }

        if (doc.RootElement.TryGetProperty("user", out var userElement)
            && userElement.TryGetProperty("email", out var emailElement)
            && emailElement.ValueKind == JsonValueKind.String)
        {
            return emailElement.GetString();
        }

        return null;
    }
}
