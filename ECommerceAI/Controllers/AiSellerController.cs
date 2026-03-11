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
}
