using System.Security.Claims;
using ECommerceAI.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAI.Middleware;

/// <summary>
/// Enriches the JWT claims with the actual user role from the database.
/// The Supabase JWT only carries role="authenticated"; this middleware
/// replaces it with the real role (admin / seller / customer).
/// Uses raw SQL to avoid EF Core table-sharing conflicts with AppUser.
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

    public async Task InvokeAsync(HttpContext context, AiDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirstValue("sub")
                   ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (Guid.TryParse(sub, out var userId))
            {
                try
                {
                    var roleCode = await dbContext.Database
                        .SqlQueryRaw<string>(
                            "SELECT r.code AS \"Value\" FROM users u JOIN roles r ON r.id = u.role_id WHERE u.id = {0}",
                            userId)
                        .OrderBy(x => x)
                        .FirstOrDefaultAsync();

                    if (roleCode != null && context.User.Identity is ClaimsIdentity identity)
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
