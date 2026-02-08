using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;

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

    public async Task InvokeAsync(HttpContext context, IUserClaimsService userClaimsService, IUserRepository userRepository)
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
                        // Auto-create user in local database
                        var newUser = new User
                        {
                            Id = userClaims.UserId,
                            FullName = userClaims.FullName,
                            Role = "customer", // Default role
                            Status = 1, // Active
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await userRepository.CreateAsync(newUser);
                        
                        _logger.LogInformation(
                            "Auto-created user in local database: {UserId}, Email: {Email}", 
                            userClaims.UserId, 
                            userClaims.Email
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error auto-creating user {UserId}", userClaims.UserId);
                    // Don't block the request if user creation fails
                    // The user is still authenticated via Supabase
                }
            }
        }

        await _next(context);
    }
}
