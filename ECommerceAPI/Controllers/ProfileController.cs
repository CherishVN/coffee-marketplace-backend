using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Requires Supabase JWT token
public class ProfileController : ControllerBase
{
    private readonly IUserClaimsService _userClaimsService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IUserClaimsService userClaimsService,
        IUserRepository userRepository,
        ILogger<ProfileController> logger)
    {
        _userClaimsService = userClaimsService;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get current user profile
    /// Example: GET /api/profile/me
    /// Requires: Authorization: Bearer {supabase_access_token}
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        // Extract user ID from Supabase JWT claims
        var userId = _userClaimsService.GetUserId();
        
        if (userId == null)
        {
            return Unauthorized(new { message = "Invalid token" });
        }

        // Get user from local database
        var user = await _userRepository.GetByIdAsync(userId.Value);
        
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Get email from claims (not stored in local DB)
        var email = _userClaimsService.GetEmail();

        return Ok(new
        {
            id = user.Id,
            email = email,
            fullName = user.FullName,
            phone = user.Phone,
            role = user.Role,
            status = user.Status,
            createdAt = user.CreatedAt
        });
    }

    /// <summary>
    /// Update current user profile
    /// Example: PUT /api/profile/me
    /// </summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        
        if (userId == null)
        {
            return Unauthorized(new { message = "Invalid token" });
        }

        var user = await _userRepository.GetByIdAsync(userId.Value);
        
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Update user profile
        user.FullName = dto.FullName ?? user.FullName;
        user.Phone = dto.Phone ?? user.Phone;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        return Ok(new
        {
            message = "Profile updated successfully",
            user = new
            {
                id = user.Id,
                fullName = user.FullName,
                phone = user.Phone,
                role = user.Role
            }
        });
    }

    /// <summary>
    /// Example endpoint to demonstrate extracting user claims
    /// Shows all available claims from Supabase JWT
    /// </summary>
    [HttpGet("claims")]
    public IActionResult GetClaims()
    {
        var claims = User.Claims.Select(c => new
        {
            type = c.Type,
            value = c.Value
        });

        var userClaims = _userClaimsService.GetUserClaims();

        return Ok(new
        {
            allClaims = claims,
            extractedClaims = userClaims
        });
    }
}

public class UpdateProfileDto
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
}
