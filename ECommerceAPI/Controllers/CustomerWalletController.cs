using ECommerceAPI.Application.DTOs.Customer;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/customer-wallet")]
[Authorize]
public class CustomerWalletController : ControllerBase
{
    private readonly ICustomerWalletService _walletService;
    private readonly IUserClaimsService _userClaimsService;

    public CustomerWalletController(ICustomerWalletService walletService, IUserClaimsService userClaimsService)
    {
        _walletService = walletService;
        _userClaimsService = userClaimsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetWallet()
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var wallet = await _walletService.GetWalletAsync(userId.Value);
        return Ok(new { success = true, data = wallet });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _walletService.GetTransactionsAsync(userId.Value, page, pageSize);
        return Ok(result);
    }

    [HttpPost("withdrawal-requests")]
    public async Task<IActionResult> CreateWithdrawalRequest([FromBody] CreateCustomerWithdrawalDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _walletService.CreateWithdrawalRequestAsync(userId.Value, dto);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("withdrawal-requests")]
    public async Task<IActionResult> GetWithdrawalRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _walletService.GetWithdrawalRequestsAsync(userId.Value, page, pageSize);
        return Ok(result);
    }
}
