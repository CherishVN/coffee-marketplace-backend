using System.Security.Claims;
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
        // Supabase stores user metadata in claims with prefix "user_metadata_"
        var fullName = _httpContextAccessor.HttpContext?.User?.FindFirst("user_metadata_full_name")?.Value;
        
        if (string.IsNullOrEmpty(fullName))
        {
            // Fallback to name claim
            fullName = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value
                      ?? _httpContextAccessor.HttpContext?.User?.FindFirst("name")?.Value;
        }

        return fullName;
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
