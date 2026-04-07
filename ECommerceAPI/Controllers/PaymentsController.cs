using ECommerceAPI.Application.DTOs.Payments;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

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

        var frontendUrl = _configuration["FrontendUrl"];

        if (result.Success)
            return Redirect($"{frontendUrl}/payment/success?orderCode={result.OrderCode}&amount={result.Amount}");
        else
            return Redirect($"{frontendUrl}/payment/failed?message={Uri.EscapeDataString(result.Message ?? "Thanh toán thất bại")}");
    }

    [HttpPost("momo/create")]
    [Authorize]
    [EnableRateLimiting("PaymentCreatePerUser")]
    public async Task<IActionResult> CreateMoMoPayment([FromBody] CreatePaymentDto dto)
    {
        var customerId = _userClaims.GetUserId();
        if (customerId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _paymentService.CreateMoMoPaymentAsync(dto.OrderId, customerId.Value);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// MoMo IPN callback - MoMo gọi về sau khi thanh toán
    /// </summary>
    [HttpPost("momo/ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> MoMoIpn([FromBody] MoMoIpnRequest request)
    {
        _logger.LogInformation("[MoMo IPN] Received callback: {Body}", JsonSerializer.Serialize(request));

        var result = await _paymentService.ProcessMoMoIpnAsync(request);

        return Ok(new { resultCode = result.Success ? 0 : -1, message = result.Message });
    }

    /// <summary>
    /// MoMo redirect về sau khi thanh toán (Return URL)
    /// </summary>
    [HttpGet("momo/return")]
    [AllowAnonymous]
    public async Task<IActionResult> MoMoReturn()
    {
        _logger.LogInformation("[MoMo Return] Received: {QueryString}", Request.QueryString.Value);

        var result = await _paymentService.ProcessMoMoReturnAsync(Request.Query);

        var frontendUrl = _configuration["FrontendUrl"];

        if (result.Success)
            return Redirect($"{frontendUrl}/payment/success?orderCode={result.OrderCode}&amount={result.Amount}");
        else
            return Redirect($"{frontendUrl}/payment/failed?message={Uri.EscapeDataString(result.Message ?? "Thanh toán thất bại")}");
    }
}
