using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

/// <summary>Proxy đọc CCCD/CMND qua FPT.AI — API key giữ ở server.</summary>
[ApiController]
[Route("api/ocr")]
[Authorize]
public class OcrController : ControllerBase
{
    private const long MaxBytes = 5L * 1024 * 1024;

    private readonly IFptVietnamIdCardOcrService _ocr;
    private readonly ILogger<OcrController> _logger;

    public OcrController(IFptVietnamIdCardOcrService ocr, ILogger<OcrController> logger)
    {
        _ocr = ocr;
        _logger = logger;
    }

    [HttpPost("vnm-id-card")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes)]
    public async Task<IActionResult> ReadVietnamIdCard(
        [FromForm(Name = "image")] IFormFile image,
        CancellationToken cancellationToken)
    {
        if (image == null || image.Length < 1)
        {
            return BadRequest(new { success = false, message = "Thiếu file ảnh (form field: image)" });
        }

        if (image.Length > MaxBytes)
        {
            return BadRequest(new { success = false, message = "Ảnh không được vượt quá 5 MB" });
        }

        if (!string.IsNullOrEmpty(image.ContentType) &&
            !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "File phải là ảnh (image/*)" });
        }

        await using var stream = image.OpenReadStream();
        var result = await _ocr.RecognizeAsync(stream, image.FileName, image.ContentType, cancellationToken);

        if (result.ErrorCode != 0)
        {
            _logger.LogDebug("FPT.AI id reader error {Code} {Message}", result.ErrorCode, result.ErrorMessage);
            return Ok(new
            {
                success = false,
                errorCode = result.ErrorCode,
                message = MapFptUserMessage(result.ErrorCode, result.ErrorMessage),
                data = (object?)null
            });
        }

        return Ok(new
        {
            success = true,
            errorCode = 0,
            message = (string?)null,
            data = result.Data
        });
    }

    private static string MapFptUserMessage(int code, string raw) =>
        code switch
        {
            0 => raw,
            1 => "Yêu cầu thiếu tham số hợp lệ (ảnh hoặc API key).",
            2 => "Ảnh không thấy đủ góc thẻ, không cắt chuẩn được. Chụp lại cho thẻ nằm gọn, đủ góc.",
            3 => "Không tìm thấy thẻ trong ảnh hoặc ảnh mờ/tối quá.",
            7 => "File không phải định dạng ảnh hợp lệ.",
            8 => "File ảnh hỏng hoặc định dạng không hỗ trợ.",
            -1 => raw,
            _ => string.IsNullOrWhiteSpace(raw) ? "Không đọc được thông tin từ ảnh." : raw
        };
}
