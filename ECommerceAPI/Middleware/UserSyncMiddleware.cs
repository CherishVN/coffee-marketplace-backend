using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ECommerceAPI.Middleware;

/// <summary>
/// Middleware to automatically create user in local database on first authenticated request
/// </summary>
public class UserSyncMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserSyncMiddleware> _logger;

    public UserSyncMiddleware(RequestDelegate next, ILogger<UserSyncMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserClaimsService userClaimsService, IUserRepository userRepository, ApplicationDbContext dbContext)
    {
        // Only process if user is authenticated
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userClaims = userClaimsService.GetUserClaims();
            
            if (userClaims != null)
            {
                try
                {
                    // Check if user exists in local database
                    var existingUser = await userRepository.GetByIdAsync(userClaims.UserId);
                    
                    if (existingUser == null)
                    {
                        // Look up the default "customer" role
                        var customerRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Code == "customer");

                        var seqValue = await dbContext.Database
                            .SqlQueryRaw<long>("SELECT nextval('users_code_seq') AS \"Value\"")
                            .FirstAsync();
                        var userCode = $"USR{seqValue:D4}";

                        var newUser = new User
                        {
                            Id = userClaims.UserId,
                            UserCode = userCode,
                            FullName = userClaims.FullName,
                            RoleId = customerRole?.Id,
                            Status = 1, // Active
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await userRepository.CreateAsync(newUser);
                        _logger.LogInformation("Auto-created user {UserId} with role customer", userClaims.UserId);
                        newUser.Role = customerRole;
                        existingUser = newUser;
                    }
                    
                    // Enrich claims with role from database
                    if (existingUser != null && context.User.Identity is System.Security.Claims.ClaimsIdentity identity)
                    {
                        // Remove existing role claim (from Supabase JWT which is "authenticated")
                        var existingRoleClaim = identity.FindFirst(System.Security.Claims.ClaimTypes.Role);
                        if (existingRoleClaim != null)
                        {
                            identity.RemoveClaim(existingRoleClaim);
                        }
                        
                        // Add role claim from database
                        var roleCode = existingUser.Role?.Code ?? "customer";
                        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, roleCode));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing user {UserId}", userClaims.UserId);
                    // Don't block the request if user processing fails
                    // The user is still authenticated via Supabase
                }
            }
        }

        await _next(context);
    }
}
