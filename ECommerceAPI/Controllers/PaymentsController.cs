using ECommerceAPI.Application.DTOs.Payments;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IUserClaimsService _userClaims;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IConfiguration _configuration;

    public PaymentsController(
        IPaymentService paymentService,
        IUserClaimsService userClaims,
        ILogger<PaymentsController> logger,
        IConfiguration configuration)
    {
        _paymentService = paymentService;
        _userClaims = userClaims;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Tạo URL thanh toán VNPay cho một đơn hàng
    /// </summary>
    [HttpPost("vnpay/create")]
    [Authorize]
    public async Task<IActionResult> CreateVNPayPayment([FromBody] CreatePaymentDto dto)
    {
        var customerId = _userClaims.GetUserId();
        if (customerId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var result = await _paymentService.CreateVNPayPaymentAsync(dto.OrderId, customerId.Value, ipAddress);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// VNPay redirect về sau khi user thanh toán (Return URL)
    /// </summary>
    [HttpGet("vnpay/return")]
    [AllowAnonymous]
    public async Task<IActionResult> VNPayReturn()
    {
        _logger.LogInformation("[VNPay Return] Received: {QueryString}", Request.QueryString.Value);

        var result = await _paymentService.ProcessVNPayReturnAsync(Request.Query);

        var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";

        if (result.Success)
            return Redirect($"{frontendUrl}/payment/success?orderId={result.OrderId}&amount={result.Amount}");
        else
            return Redirect($"{frontendUrl}/payment/failed?message={Uri.EscapeDataString(result.Message ?? "Thanh toán thất bại")}");
    }
}
