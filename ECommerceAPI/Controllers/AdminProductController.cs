using System.Security.Claims;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "admin")]
public class AdminProductController : ControllerBase
{
    private readonly IProductModerationService _productModerationService;

    public AdminProductController(IProductModerationService productModerationService)
    {
        _productModerationService = productModerationService;
    }

    /// <summary>
    /// Get all products for moderation
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? status = null,
        [FromQuery] Guid? shopId = null,
        [FromQuery] string? search = null)
    {
        var result = await _productModerationService.GetAllProductsAsync(page, pageSize, status, shopId, search);
        return Ok(result);
    }

    /// <summary>
    /// Get product details
    /// </summary>
    [HttpGet("{productId}")]
    public async Task<IActionResult> GetProductById(Guid productId)
    {
        var result = await _productModerationService.GetProductByIdAsync(productId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Hide product (violations)
    /// </summary>
    [HttpPost("{productId}/hide")]
    public async Task<IActionResult> HideProduct(Guid productId, [FromBody] HideProductDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _productModerationService.HideProductAsync(productId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Unhide product
    /// </summary>
    [HttpPost("{productId}/unhide")]
    public async Task<IActionResult> UnhideProduct(Guid productId)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _productModerationService.UnhideProductAsync(productId, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Remove product permanently
    /// </summary>
    [HttpPost("{productId}/remove")]
    public async Task<IActionResult> RemoveProduct(Guid productId, [FromBody] RemoveProductDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _productModerationService.RemoveProductAsync(productId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
