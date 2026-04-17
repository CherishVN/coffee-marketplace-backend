using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/seller/materials")]
[Authorize(Roles = "seller,admin")]
public class SellerMaterialController : ControllerBase
{
    private readonly IMaterialAdminService _materialService;

    public SellerMaterialController(IMaterialAdminService materialService)
    {
        _materialService = materialService;
    }

    /// <summary>Lấy danh sách chất liệu đang active (dành cho seller chọn khi tạo sản phẩm)</summary>
    [HttpGet]
    public async Task<IActionResult> GetMaterials(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        [FromQuery] string? search = null)
    {
        var result = await _materialService.GetAllMaterialsAsync(page, pageSize, search, isActive: true);
        return Ok(result);
    }
}
