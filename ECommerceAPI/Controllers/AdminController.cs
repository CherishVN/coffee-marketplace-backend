using System.Security.Claims;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly IUserAdminService _userAdminService;

    public AdminController(IUserAdminService userAdminService)
    {
        _userAdminService = userAdminService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? role = null,
        [FromQuery] short? status = null)
    {
        var result = await _userAdminService.GetAllUsersAsync(page, pageSize, role, status);
        return Ok(result);
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserById(Guid userId)
    {
        var result = await _userAdminService.GetUserByIdAsync(userId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpPut("{userId}")]
    public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserDto dto)
    {
        var editorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _userAdminService.UpdateUserAsync(userId, dto, editorId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{userId}/suspend")]
    public async Task<IActionResult> SuspendUser(Guid userId, [FromBody] SuspendUserDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _userAdminService.SuspendUserAsync(userId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{userId}/unsuspend")]
    public async Task<IActionResult> UnsuspendUser(Guid userId)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _userAdminService.UnsuspendUserAsync(userId, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("{userId}/audit-logs")]
    public async Task<IActionResult> GetUserAuditLogs(Guid userId)
    {
        var result = await _userAdminService.GetUserAuditLogsAsync(userId);
        return Ok(result);
    }

    [HttpGet("{userId}/addresses")]
    public async Task<IActionResult> GetUserAddresses(Guid userId)
    {
        var result = await _userAdminService.GetUserAddressesAsync(userId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpGet("{userId}/wallet")]
    public async Task<IActionResult> GetUserWallet(Guid userId)
    {
        var result = await _userAdminService.GetUserWalletDetailsAsync(userId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpGet("{userId}/reviews/products")]
    public async Task<IActionResult> GetUserProductReviews(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _userAdminService.GetUserProductReviewsAsync(userId, page, pageSize);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpGet("{userId}/reviews/shops")]
    public async Task<IActionResult> GetUserShopReviews(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _userAdminService.GetUserShopReviewsAsync(userId, page, pageSize);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpPut("{userId}/account-status")]
    public async Task<IActionResult> UpdateAccountStatus(Guid userId, [FromBody] UpdateUserAccountStatusDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _userAdminService.UpdateUserAccountStatusAsync(userId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{userId}/send-password-reset")]
    public async Task<IActionResult> SendPasswordReset(Guid userId)
    {
        var result = await _userAdminService.SendPasswordResetEmailAsync(userId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
