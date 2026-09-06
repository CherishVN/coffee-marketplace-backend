using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/seller/tags")]
[Authorize(Roles = "seller,admin")]
public class SellerTagController : ControllerBase
{
    private readonly ITagAdminService _tagAdminService;

    public SellerTagController(ITagAdminService tagAdminService)
    {
        _tagAdminService = tagAdminService;
    }

    /// <summary>Lấy danh sách thẻ (dành cho seller chọn khi tạo sản phẩm)</summary>
    [HttpGet]
    public async Task<IActionResult> GetTags(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 300,
        [FromQuery] string? search = null)
    {
        var result = await _tagAdminService.GetAllTagsAsync(page, pageSize, search);
        return Ok(result);
    }
}
