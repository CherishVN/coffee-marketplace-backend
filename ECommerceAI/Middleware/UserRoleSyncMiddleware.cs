using System.Security.Claims;
using ECommerceAI.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAI.Middleware;

/// <summary>
/// Enriches the JWT claims with the actual user role from the database.
/// The Supabase JWT only carries role="authenticated"; this middleware
/// replaces it with the real role (admin / seller / customer).
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
                    var user = await dbContext.UsersReadOnly
                        .Include(u => u.Role)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == userId);

                    if (user?.Role != null && context.User.Identity is ClaimsIdentity identity)
                    {
                        var existing = identity.FindFirst(ClaimTypes.Role);
                        if (existing != null) identity.RemoveClaim(existing);
                        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.Code));
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
