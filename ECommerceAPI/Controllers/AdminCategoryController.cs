using System.Security.Claims;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = "admin")]
public class AdminCategoryController : ControllerBase
{
    private readonly ICategoryAdminService _categoryAdminService;

    public AdminCategoryController(ICategoryAdminService categoryAdminService)
    {
        _categoryAdminService = categoryAdminService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCategories(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? level = null,
        [FromQuery] bool? isActive = null)
    {
        var result = await _categoryAdminService.GetAllCategoriesAsync(page, pageSize, level, isActive);
        return Ok(result);
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetCategoryTree()
    {
        var result = await _categoryAdminService.GetCategoryTreeAsync();
        return Ok(result);
    }

    [HttpGet("{categoryId}")]
    public async Task<IActionResult> GetCategoryById(long categoryId)
    {
        var result = await _categoryAdminService.GetCategoryByIdAsync(categoryId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _categoryAdminService.CreateCategoryAsync(dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return CreatedAtAction(nameof(GetCategoryById), new { categoryId = result.Category?.Id }, result);
    }

    [HttpPut("{categoryId}")]
    public async Task<IActionResult> UpdateCategory(long categoryId, [FromBody] UpdateCategoryDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _categoryAdminService.UpdateCategoryAsync(categoryId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{categoryId}/activate")]
    public async Task<IActionResult> ActivateCategory(long categoryId)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _categoryAdminService.ActivateCategoryAsync(categoryId, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{categoryId}/deactivate")]
    public async Task<IActionResult> DeactivateCategory(long categoryId, [FromBody] ToggleCategoryStatusDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _categoryAdminService.DeactivateCategoryAsync(categoryId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{categoryId}")]
    public async Task<IActionResult> DeleteCategory(long categoryId)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _categoryAdminService.DeleteCategoryAsync(categoryId, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{categoryId}/migrate-products")]
    public async Task<IActionResult> MigrateProducts(long categoryId, [FromBody] MigrateProductsDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _categoryAdminService.MigrateProductsAsync(categoryId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
