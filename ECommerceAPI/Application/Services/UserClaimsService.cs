using System.Security.Claims;
using System.Text.Json;
using ECommerceAPI.Application.DTOs.Auth;
using ECommerceAPI.Application.Interfaces;

namespace ECommerceAPI.Application.Services;

public class UserClaimsService : IUserClaimsService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserClaimsService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private static ClaimsPrincipal? Principal(IHttpContextAccessor accessor) =>
        accessor.HttpContext?.User;

    public Guid? GetUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return null;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    public string? GetEmail()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value
               ?? _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value;
    }

    public string? GetFullName()
    {
        var user = Principal(_httpContextAccessor);
        if (user == null)
            return null;

        // Some setups flatten metadata into separate claims
        var fullName = user.FindFirst("user_metadata_full_name")?.Value;
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName.Trim();

        // Supabase JWT: user_metadata is typically one JSON claim (full_name from signUp lives here)
        foreach (var claim in user.Claims)
        {
            if (claim.Type is not ("user_metadata" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/user_metadata"))
                continue;

            var fromJson = TryGetFullNameFromUserMetadataJson(claim.Value);
            if (!string.IsNullOrWhiteSpace(fromJson))
                return fromJson.Trim();
        }

        fullName = user.FindFirst(ClaimTypes.Name)?.Value
                   ?? user.FindFirst("name")?.Value;

        return string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
    }

    private static string? TryGetFullNameFromUserMetadataJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("full_name", out var fullNameEl) && fullNameEl.ValueKind == JsonValueKind.String)
            {
                var s = fullNameEl.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }

            if (root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            {
                var s = nameEl.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
        }
        catch (JsonException)
        {
            // ignore malformed claim
        }

        return null;
    }

    public UserClaimsDto? GetUserClaims()
    {
        var userId = GetUserId();
        if (userId == null)
            return null;

        return new UserClaimsDto
        {
            UserId = userId.Value,
            Email = GetEmail(),
            FullName = GetFullName()
        };
    }
}
