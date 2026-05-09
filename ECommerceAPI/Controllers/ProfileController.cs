using System.Security.Claims;
using ECommerceAPI.Application.DTOs.User;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IUserClaimsService _userClaimsService;
    private readonly IUserProfileService _userProfileService;
    private readonly IUserAuthEmailResolver _authEmailResolver;

    public ProfileController(
        IUserClaimsService userClaimsService,
        IUserProfileService userProfileService,
        IUserAuthEmailResolver authEmailResolver)
    {
        _userClaimsService = userClaimsService;
        _userProfileService = userProfileService;
        _authEmailResolver = authEmailResolver;
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = _userClaimsService.GetUserId();
        
        if (userId == null)
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        try
        {
            var profile = await _userProfileService.GetProfileAsync(userId.Value);
            if (string.IsNullOrWhiteSpace(profile.Email))
            {
                profile.Email = _userClaimsService.GetEmail();
            }

            return Ok(new { success = true, data = profile });
        }
        catch (Exception ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Update current user profile
    /// </summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        
        if (userId == null)
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var result = await _userProfileService.UpdateProfileAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    /// <summary>
    /// Register as seller
    /// </summary>
    [HttpPost("register-seller")]
    public async Task<IActionResult> RegisterAsSeller([FromBody] RegisterSellerDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        
        if (userId == null)
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var result = await _userProfileService.RegisterAsSellerAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    /// <summary>
    /// Get user addresses
    /// </summary>
    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses()
    {
        var userId = _userClaimsService.GetUserId();
        
        if (userId == null)
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var addresses = await _userProfileService.GetAddressesAsync(userId.Value);

        return Ok(new { success = true, data = addresses });
    }

    /// <summary>
    /// Add new address
    /// </summary>
    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress([FromBody] AddAddressDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        
        if (userId == null)
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var result = await _userProfileService.AddAddressAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    /// <summary>
    /// Update address
    /// </summary>
    [HttpPut("addresses/{addressId}")]
    public async Task<IActionResult> UpdateAddress(Guid addressId, [FromBody] UpdateAddressDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        
        if (userId == null)
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var result = await _userProfileService.UpdateAddressAsync(userId.Value, addressId, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    /// <summary>
    /// Delete address
    /// </summary>
    [HttpDelete("addresses/{addressId}")]
    public async Task<IActionResult> DeleteAddress(Guid addressId)
    {
        var userId = _userClaimsService.GetUserId();
        
        if (userId == null)
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var result = await _userProfileService.DeleteAddressAsync(userId.Value, addressId);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    /// <summary>
    /// Set default address
    /// </summary>
    [HttpPost("addresses/{addressId}/set-default")]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId)
    {
        var userId = _userClaimsService.GetUserId();
        
        if (userId == null)
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var result = await _userProfileService.SetDefaultAddressAsync(userId.Value, addressId);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    [HttpPost("profile/request-email-change")]
    public async Task<IActionResult> RequestEmailChange([FromBody] RequestEmailChangeDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        var currentEmail = _userClaimsService.GetEmail() ?? string.Empty;

        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _userProfileService.RequestEmailChangeAsync(userId.Value, currentEmail, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }
    [HttpPost("profile/confirm-email-change")]
    public async Task<IActionResult> ConfirmEmailChange([FromBody] ConfirmEmailChangeDto dto)
    {
        var userId = _userClaimsService.GetUserId();

        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _userProfileService.ConfirmEmailChangeAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    /// <summary>
    /// Xóa cache snapshot Supabase Auth của user hiện tại. Gọi sau khi user
    /// vừa cập nhật avatar (avatar_storage_path) hoặc metadata khác để các
    /// service đọc avatar (review, conversation, ...) lấy dữ liệu mới ngay.
    /// </summary>
    [HttpPost("profile/refresh-auth-cache")]
    public IActionResult RefreshAuthCache()
    {
        var userId = _userClaimsService.GetUserId();
        if (!userId.HasValue)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        _authEmailResolver.InvalidateUser(userId.Value);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Get current user claims (for debugging)
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
            success = true,
            allClaims = claims,
            extractedClaims = userClaims
        });
    }
}
