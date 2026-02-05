using System.Security.Claims;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/disputes")]
[Authorize(Roles = "admin")]
public class AdminDisputeController : ControllerBase
{
    private readonly IDisputeAdminService _disputeAdminService;

    public AdminDisputeController(IDisputeAdminService disputeAdminService)
    {
        _disputeAdminService = disputeAdminService;
    }

    /// <summary>
    /// Get all disputes for admin review
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllDisputes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? status = null,
        [FromQuery] short? type = null)
    {
        var result = await _disputeAdminService.GetAllDisputesAsync(page, pageSize, status, type);
        return Ok(result);
    }

    /// <summary>
    /// Get dispute details
    /// </summary>
    [HttpGet("{disputeId}")]
    public async Task<IActionResult> GetDisputeById(Guid disputeId)
    {
        var result = await _disputeAdminService.GetDisputeByIdAsync(disputeId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Approve refund for dispute
    /// </summary>
    [HttpPost("{disputeId}/approve-refund")]
    public async Task<IActionResult> ApproveRefund(Guid disputeId, [FromBody] ApproveRefundDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _disputeAdminService.ApproveRefundAsync(disputeId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Reject dispute
    /// </summary>
    [HttpPost("{disputeId}/reject")]
    public async Task<IActionResult> RejectDispute(Guid disputeId, [FromBody] RejectDisputeDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _disputeAdminService.RejectDisputeAsync(disputeId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
