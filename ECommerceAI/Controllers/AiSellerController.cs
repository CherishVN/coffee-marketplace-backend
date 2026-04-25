using ECommerceAI.DTOs.Seller;
using ECommerceAI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECommerceAI.Controllers;

[ApiController]
[Route("api/ai/seller")]
[Authorize]
public class AiSellerController : ControllerBase
{
    private readonly IAiSellerService _sellerService;

    public AiSellerController(IAiSellerService sellerService)
    {
        _sellerService = sellerService;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Không xác định được user"));

    /// <summary>Gợi ý category cho sản phẩm</summary>
    [HttpPost("suggest-category")]
    public async Task<IActionResult> SuggestCategory([FromBody] SuggestCategoryRequestDto dto)
    {
        var sellerId = GetUserId();
        var result = await _sellerService.SuggestCategoryAsync(dto, sellerId);
        return Ok(result);
    }

    /// <summary>Gợi ý tags cho sản phẩm</summary>
    [HttpPost("suggest-tags")]
    public async Task<IActionResult> SuggestTags([FromBody] SuggestTagsRequestDto dto)
    {
        var sellerId = GetUserId();
        var result = await _sellerService.SuggestTagsAsync(dto, sellerId);
        return Ok(result);
    }

    /// <summary>Gợi ý chất liệu (materials) cho sản phẩm</summary>
    [HttpPost("suggest-materials")]
    public async Task<IActionResult> SuggestMaterials([FromBody] SuggestMaterialsRequestDto dto)
    {
        var sellerId = GetUserId();
        var result = await _sellerService.SuggestMaterialsAsync(dto, sellerId);
        return Ok(result);
    }

    /// <summary>
    /// Lưu phản hồi sau khi seller chọn tags từ gợi ý AI.
    /// Gọi endpoint này sau khi đã có logId từ suggest-tags (chỉ khi gọi suggest-tags kèm productId).
    /// </summary>
    [HttpPost("tag-feedback")]
    public async Task<IActionResult> SaveTagFeedback([FromBody] SaveSuggestionFeedbackDto dto)
    {
        var sellerId = GetUserId();
        var success = await _sellerService.SaveTagSuggestionFeedbackAsync(dto, sellerId);
        if (!success)
            return NotFound(new { message = "Không tìm thấy bản ghi gợi ý. Hãy chắc chắn suggest-tags được gọi kèm productId." });

        return Ok(new { message = "Đã lưu phản hồi thành công" });
    }

    /// <summary>
    /// Lưu phản hồi sau khi seller chọn materials từ gợi ý AI.
    /// Gọi endpoint này sau khi đã có logId từ suggest-materials (chỉ khi gọi suggest-materials kèm productId).
    /// </summary>
    [HttpPost("material-feedback")]
    public async Task<IActionResult> SaveMaterialFeedback([FromBody] SaveMaterialFeedbackDto dto)
    {
        var sellerId = GetUserId();
        var success = await _sellerService.SaveMaterialSuggestionFeedbackAsync(dto, sellerId);
        if (!success)
            return NotFound(new { message = "Không tìm thấy bản ghi gợi ý. Hãy chắc chắn suggest-materials được gọi kèm productId." });

        return Ok(new { message = "Đã lưu phản hồi thành công" });
    }

    /// <summary>
    /// Ghi lịch sử gợi ý tag và/hoặc chất liệu sau khi tạo sản phẩm (analyze-product / analyze-image).
    /// </summary>
    [HttpPost("commit-product-ai-tags")]
    public async Task<IActionResult> CommitProductAiTagSession([FromBody] CommitProductAiTagSessionDto dto)
    {
        if (dto.ProductId == Guid.Empty)
            return BadRequest(new { message = "productId không hợp lệ." });
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Tên sản phẩm là bắt buộc." });

        var sellerId = GetUserId();
        var ok = await _sellerService.CommitProductAiTagSessionAsync(dto, sellerId);
        if (!ok)
            return NotFound(new { message = "Không tìm thấy sản phẩm hoặc bạn không có quyền." });

        return Ok(new { message = "Đã lưu lịch sử gợi ý." });
    }

    /// <summary>Lấy lịch sử gợi ý tags của seller (không gồm pending)</summary>
    [HttpGet("tag-suggestions")]
    public async Task<IActionResult> GetTagSuggestions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var sellerId = GetUserId();
        var result = await _sellerService.GetTagSuggestionLogsAsync(sellerId, page, pageSize);
        return Ok(result);
    }

    /// <summary>Lấy lịch sử gợi ý chất liệu của seller (không gồm pending)</summary>
    [HttpGet("material-suggestions")]
    public async Task<IActionResult> GetMaterialSuggestions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var sellerId = GetUserId();
        var result = await _sellerService.GetMaterialSuggestionLogsAsync(sellerId, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Phân tích sản phẩm (text-only) — trả về category + tags + materials trong 1 lần gọi Gemini.
    /// Thay thế cho việc gọi riêng suggest-category → suggest-tags → suggest-materials (3 calls → 1 call).
    /// </summary>
    [HttpPost("analyze-product")]
    public async Task<IActionResult> AnalyzeProduct([FromBody] AnalyzeProductRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Tên sản phẩm là bắt buộc." });

        var sellerId = GetUserId();
        var result = await _sellerService.AnalyzeProductAsync(dto, sellerId);

        if (!result.Success)
            return AiErrorResult(result.ErrorMessage);

        return Ok(result);
    }

    /// <summary>
    /// Phân tích ảnh sản phẩm bằng Gemini Vision.
    /// Trả về: đánh giá chất lượng ảnh, gợi ý category/tags/materials, đề xuất cải thiện.
    /// </summary>
    [HttpPost("analyze-image")]
    public async Task<IActionResult> AnalyzeImage([FromBody] AnalyzeImageRequestDto dto)
    {
        if (dto.ImageUrls == null || dto.ImageUrls.Count == 0)
            return BadRequest(new { message = "Vui lòng cung cấp ít nhất 1 URL ảnh." });

        if (dto.ImageUrls.Count > 3)
            return BadRequest(new { message = "Tối đa 3 ảnh mỗi lần phân tích." });

        var invalidUrls = dto.ImageUrls
            .Where(u =>
                !u.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) &&
                (!Uri.TryCreate(u, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            .ToList();

        if (invalidUrls.Any())
            return BadRequest(new
            {
                message = "Một số ảnh không hợp lệ. Chỉ hỗ trợ http/https hoặc data:image/...;base64",
                invalidUrls
            });

        var sellerId = GetUserId();
        var result = await _sellerService.AnalyzeImageAsync(dto, sellerId);

        if (!result.Success)
            return AiErrorResult(result.ErrorMessage);

        return Ok(result);
    }

    private IActionResult AiErrorResult(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return BadRequest(new { message = "Lỗi AI." });
        var t = errorMessage;
        if (t.Contains("không phản hồi", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("quá tải", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("server overload", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("vượt giới hạn", StringComparison.OrdinalIgnoreCase))
        {
            return new ObjectResult(new { message = errorMessage }) { StatusCode = 503 };
        }
        return BadRequest(new { message = errorMessage });
    }
}
