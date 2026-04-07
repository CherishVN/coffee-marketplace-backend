using System.Globalization;
using System.Text.Json;
using ECommerceAPI.Application.DTOs.Payments;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IUserClaimsService _userClaims;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;

    public PaymentsController(
        IPaymentService paymentService,
        IUserClaimsService userClaims,
        ILogger<PaymentsController> logger,
        IConfiguration configuration,
        IMemoryCache memoryCache)
    {
        _paymentService = paymentService;
        _userClaims = userClaims;
        _logger = logger;
        _configuration = configuration;
        _memoryCache = memoryCache;
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

        var result = await _paymentService.CreateVNPayPaymentAsync(
            dto.OrderId,
            customerId.Value,
            ipAddress,
            dto.ClientReturnSuccessUrl,
            dto.ClientReturnFailureUrl);

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

        var txnRef = Request.Query["vnp_TxnRef"].ToString();
        _memoryCache.TryGetValue($"VnpayClientSuccess_{txnRef}", out string? clientSuccess);
        _memoryCache.TryGetValue($"VnpayClientFailure_{txnRef}", out string? clientFailure);

        var result = await _paymentService.ProcessVNPayReturnAsync(Request.Query);

        var frontendUrl = (_configuration["FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');

        if (result.Success && result.OrderId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(clientSuccess))
            {
                var url = QueryHelpers.AddQueryString(clientSuccess.Trim(), new Dictionary<string, string?>
                {
                    ["orderId"] = result.OrderId.Value.ToString(),
                    ["amount"] = result.Amount.ToString(CultureInfo.InvariantCulture)
                });
                return Redirect(url);
            }

            return Redirect($"{frontendUrl}/payment/success?orderId={result.OrderId}&amount={result.Amount}");
        }

        var failMsg = TruncateForRedirect(result.Message ?? "Thanh toán thất bại", 500);
        if (!string.IsNullOrWhiteSpace(clientFailure))
        {
            var url = QueryHelpers.AddQueryString(clientFailure.Trim(), new Dictionary<string, string?>
            {
                ["message"] = failMsg
            });
            return Redirect(url);
        }

        return Redirect($"{frontendUrl}/payment/failed?message={Uri.EscapeDataString(failMsg)}");
    }

    private static string TruncateForRedirect(string message, int maxLen) =>
        message.Length <= maxLen ? message : message[..maxLen];

    /// <summary>
    /// Tạo URL thanh toán MoMo cho một đơn hàng
    /// </summary>
    [HttpPost("momo/create")]
    [Authorize]
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
    /// MoMo redirect về sau khi thanh toán (Return URL - trả JSON để test BE)
    /// </summary>
    [HttpGet("momo/return")]
    [AllowAnonymous]
    public IActionResult MoMoReturn([FromQuery] string orderId, [FromQuery] int resultCode, [FromQuery] string? message)
    {
        _logger.LogInformation("[MoMo Return] OrderId={OrderId}, ResultCode={Code}", orderId, resultCode);

        var frontendUrl = _configuration["FrontendUrl"];

        if (resultCode == 0)
            return Redirect($"{frontendUrl}/payment/success?orderId={orderId}");
        else
            return Redirect($"{frontendUrl}/payment/failed?message={Uri.EscapeDataString(message ?? "Thanh toán thất bại")}");
    }
}
