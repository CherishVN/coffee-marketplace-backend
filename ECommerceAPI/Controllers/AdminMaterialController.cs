using System.Security.Claims;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/materials")]
[Authorize(Roles = "admin")]
public class AdminMaterialController : ControllerBase
{
    private readonly IMaterialAdminService _materialService;

    public AdminMaterialController(IMaterialAdminService materialService)
    {
        _materialService = materialService;
    }

    /// <summary>Lấy danh sách chất liệu (có phân trang, tìm kiếm, lọc trạng thái)</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllMaterials(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
        var result = await _materialService.GetAllMaterialsAsync(page, pageSize, search, isActive);
        return Ok(result);
    }

    /// <summary>Lấy chi tiết một chất liệu theo ID</summary>
    [HttpGet("{materialId:guid}")]
    public async Task<IActionResult> GetMaterialById(Guid materialId)
    {
        var result = await _materialService.GetMaterialByIdAsync(materialId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    /// <summary>Tạo chất liệu mới</summary>
    [HttpPost]
    public async Task<IActionResult> CreateMaterial([FromBody] CreateMaterialDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { success = false, message = "Tên chất liệu không được để trống" });

        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _materialService.CreateMaterialAsync(dto, adminId);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetMaterialById), new { materialId = result.Material?.Id }, result);
    }

    /// <summary>Cập nhật thông tin chất liệu</summary>
    [HttpPut("{materialId:guid}")]
    public async Task<IActionResult> UpdateMaterial(Guid materialId, [FromBody] UpdateMaterialDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { success = false, message = "Tên chất liệu không được để trống" });

        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _materialService.UpdateMaterialAsync(materialId, dto, adminId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>Kích hoạt / Vô hiệu hóa chất liệu (toggle)</summary>
    [HttpPost("{materialId:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid materialId)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _materialService.ToggleActiveAsync(materialId, adminId);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>Xóa chất liệu (chỉ khi không có sản phẩm nào đang dùng)</summary>
    [HttpDelete("{materialId:guid}")]
    public async Task<IActionResult> DeleteMaterial(Guid materialId)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _materialService.DeleteMaterialAsync(materialId, adminId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
