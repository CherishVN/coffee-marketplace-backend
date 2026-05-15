using ECommerceAPI.Application.DTOs.Disputes;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize]
public class CustomerDisputeController : ControllerBase
{
    private readonly ICustomerDisputeService _disputeService;
    private readonly IUserClaimsService _userClaimsService;

    public CustomerDisputeController(
        ICustomerDisputeService disputeService,
        IUserClaimsService userClaimsService)
    {
        _disputeService = disputeService;
        _userClaimsService = userClaimsService;
    }

    /// <summary>
    /// Create a new complaint for a delivered/completed order (within 7 days)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateDispute([FromBody] CreateDisputeDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _disputeService.CreateDisputeAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get all my disputes
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyDisputes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] short? status = null)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _disputeService.GetMyDisputesAsync(userId.Value, page, pageSize, status);
        return Ok(result);
    }

    /// <summary>
    /// Get dispute detail
    /// </summary>
    [HttpGet("{disputeId}")]
    public async Task<IActionResult> GetDisputeById(Guid disputeId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _disputeService.GetDisputeByIdAsync(userId.Value, disputeId);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Update evidence (only allowed before admin final decision)
    /// </summary>
    [HttpPut("{disputeId}/evidence")]
    public async Task<IActionResult> UpdateEvidence(Guid disputeId, [FromBody] UpdateEvidenceDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _disputeService.UpdateEvidenceAsync(userId.Value, disputeId, dto);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Cancel a pending dispute
    /// </summary>
    [HttpPost("{disputeId}/cancel")]
    public async Task<IActionResult> CancelDispute(Guid disputeId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _disputeService.CancelDisputeAsync(userId.Value, disputeId);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Customer gửi hàng trả (cung cấp mã vận đơn)
    /// </summary>
    [HttpPost("{disputeId}/send-return")]
    public async Task<IActionResult> SendReturn(Guid disputeId, [FromBody] string trackingCode)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _disputeService.SendReturnAsync(userId.Value, disputeId, trackingCode);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
