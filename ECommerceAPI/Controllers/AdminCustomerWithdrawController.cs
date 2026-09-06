using System.Security.Claims;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/customer-withdrawals")]
[Authorize(Roles = "admin")]
public class AdminCustomerWithdrawController : ControllerBase
{
    private readonly ICustomerWalletService _walletService;

    public AdminCustomerWithdrawController(ICustomerWalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] short? status = null)
    {
        var result = await _walletService.AdminGetAllRequestsAsync(page, pageSize, status);
        return Ok(result);
    }

    [HttpPost("{requestId}/approve")]
    public async Task<IActionResult> ApproveRequest(Guid requestId, [FromBody] AdminWithdrawalActionDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _walletService.AdminApproveRequestAsync(requestId, dto.AdminNote, adminId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{requestId}/reject")]
    public async Task<IActionResult> RejectRequest(Guid requestId, [FromBody] AdminWithdrawalRejectDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _walletService.AdminRejectRequestAsync(requestId, dto.Reason, dto.AdminNote, adminId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}

public class AdminWithdrawalActionDto
{
    public string? AdminNote { get; set; }
}

public class AdminWithdrawalRejectDto
{
    public string Reason { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
}
