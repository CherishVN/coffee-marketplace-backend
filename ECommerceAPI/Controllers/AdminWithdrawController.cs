using System.Security.Claims;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/withdrawals")]
[Authorize(Roles = "admin")]
public class AdminWithdrawController : ControllerBase
{
    private readonly IWithdrawAdminService _withdrawAdminService;

    public AdminWithdrawController(IWithdrawAdminService withdrawAdminService)
    {
        _withdrawAdminService = withdrawAdminService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] short? status = null)
    {
        var result = await _withdrawAdminService.GetAllRequestsAsync(page, pageSize, status);
        return Ok(result);
    }

    [HttpGet("{requestId}")]
    public async Task<IActionResult> GetRequestById(Guid requestId)
    {
        var result = await _withdrawAdminService.GetRequestByIdAsync(requestId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpPost("{requestId}/approve")]
    public async Task<IActionResult> ApproveRequest(Guid requestId, [FromBody] ApproveWithdrawDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _withdrawAdminService.ApproveRequestAsync(requestId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{requestId}/reject")]
    public async Task<IActionResult> RejectRequest(Guid requestId, [FromBody] RejectWithdrawDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _withdrawAdminService.RejectRequestAsync(requestId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
