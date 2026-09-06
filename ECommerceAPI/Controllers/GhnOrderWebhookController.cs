using System.Text.Json;
using ECommerceAPI.Application.DTOs.Webhooks;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

/// <summary>
/// Nhận callback trạng thái vận đơn từ GHN (Order status). Phải trả HTTP 200 khi xử lý xong
/// để GHN không gửi lại (tối đa 10 lần, cách 5s).
/// </summary>
[ApiController]
[Route("api/webhooks/ghn")]
public class GhnOrderWebhookController : ControllerBase
{
    private readonly IGhnOrderWebhookService _ghnOrderWebhook;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GhnOrderWebhookController> _logger;

    public GhnOrderWebhookController(
        IGhnOrderWebhookService ghnOrderWebhook,
        IConfiguration configuration,
        ILogger<GhnOrderWebhookController> logger)
    {
        _ghnOrderWebhook = ghnOrderWebhook;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Cấu hình URL này trên GHN. Optional: cùng hàng <c>GHN:WebhookToken</c> thì gửi header
    /// <c>X-GHN-Webhook-Token</c> hoặc query <c>?token=</c> khớp token.
    /// </summary>
    [HttpPost("order-status")]
    [AllowAnonymous]
    public async Task<IActionResult> OrderStatus(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!ValidateWebhookToken())
        {
            _logger.LogWarning("GHN webhook: token không hợp lệ");
            return Unauthorized();
        }

        GhnOrderStatusPayload? payload;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            payload = JsonSerializer.Deserialize<GhnOrderStatusPayload>(body.GetRawText(), options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GHN webhook: JSON không đọc được");
            return Ok();
        }

        if (payload == null)
        {
            return Ok();
        }

        try
        {
            await _ghnOrderWebhook.ProcessOrderStatusAsync(
                payload,
                validateShopId: true,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GHN webhook: lỗi xử lý nội bộ");
        }

        return Ok();
    }

    /// <summary>
    /// Giả lập callback GHN cho môi trường dev qua Postman.
    /// </summary>
    [HttpPost("simulate")]
    [AllowAnonymous]
    public async Task<IActionResult> Simulate(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue("GHN:AllowSimulateWebhook", false))
        {
            return NotFound();
        }

        var expected = _configuration["GHN:SimulateKey"]?.Trim();
        if (string.IsNullOrEmpty(expected)
            || !Request.Headers.TryGetValue("X-Simulate-Key", out var key)
            || !string.Equals(key.ToString(), expected, StringComparison.Ordinal))
        {
            return Unauthorized();
        }


        GhnOrderStatusPayload? payload;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            payload = JsonSerializer.Deserialize<GhnOrderStatusPayload>(body.GetRawText(), options);
        }
        catch
        {
            return BadRequest(new { success = false, message = "Invalid JSON" });
        }

        if (payload == null)
            return BadRequest(new { success = false, message = "Empty body" });

        var result = await _ghnOrderWebhook.ProcessOrderStatusAsync(
            payload,
            validateShopId: false,
            cancellationToken);

        return Ok(new { success = true, result.Message, result.OrderId });
    }

    /// <summary>
    /// Giả lập callback GHN cho luồng trả hàng (Return) qua Postman.
    /// Body: { "TrackingCode": "RTN-XXXX", "Status": "returned" }
    /// </summary>
    [HttpPost("simulate-return")]
    [AllowAnonymous]
    public async Task<IActionResult> SimulateReturn(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue("GHN:AllowSimulateWebhook", false)) return NotFound();

        var expected = _configuration["GHN:SimulateKey"]?.Trim();
        if (string.IsNullOrEmpty(expected)
            || !Request.Headers.TryGetValue("X-Simulate-Key", out var key)
            || !string.Equals(key.ToString(), expected, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var input = JsonSerializer.Deserialize<Dictionary<string, object>>(body.GetRawText(), options);
            
            if (input == null || !input.TryGetValue("TrackingCode", out var tcObj))
                return BadRequest(new { success = false, message = "Missing TrackingCode" });

            var trackingCode = tcObj.ToString();
            var status = input.TryGetValue("Status", out var s) ? s.ToString() : "returned";
            
            List<string>? evidenceUrls = null;
            if (input.TryGetValue("EvidenceUrls", out var evObj) && evObj is JsonElement evEl && evEl.ValueKind == JsonValueKind.Array)
            {
                evidenceUrls = JsonSerializer.Deserialize<List<string>>(evEl.GetRawText());
            }

            // Tạo payload giả lập để gửi vào service xử lý chung
            var payload = new GhnOrderStatusPayload
            {
                OrderCode = trackingCode, 
                Status = status,
                Time = DateTime.UtcNow,
                Description = "Giả lập GHN giao trả hàng thành công",
                // Chúng ta sẽ cần mở rộng GhnOrderStatusPayload để chứa ảnh nếu muốn truyền qua Service
            };

            var result = await _ghnOrderWebhook.ProcessOrderStatusAsync(
                payload,
                validateShopId: false,
                cancellationToken,
                evidenceUrls); // Cần cập nhật Service để nhận thêm tham số này

            return Ok(new { success = true, result.Message, result.OrderId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private bool ValidateWebhookToken()
    {
        var expected = _configuration["GHN:WebhookToken"]?.Trim();
        if (string.IsNullOrEmpty(expected))
            return true;

        if (Request.Query.TryGetValue("token", out var q) && string.Equals(q.ToString(), expected, StringComparison.Ordinal))
            return true;

        if (Request.Headers.TryGetValue("X-GHN-Webhook-Token", out var h) && string.Equals(h.ToString(), expected, StringComparison.Ordinal))
            return true;

        return false;
    }
}
