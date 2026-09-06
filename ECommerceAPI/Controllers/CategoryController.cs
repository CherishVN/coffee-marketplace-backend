using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

/// <summary>
/// Public category endpoints — no authentication required.
/// Used by the customer-facing storefront navigation and filtering.
/// </summary>
[ApiController]
[Route("api/categories")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryStorefrontService _categoryStorefrontService;

    public CategoryController(ICategoryStorefrontService categoryStorefrontService)
    {
        _categoryStorefrontService = categoryStorefrontService;
    }

    /// <summary>
    /// Get paginated list of active categories.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? level = null)
    {
        var result = await _categoryStorefrontService.GetCategoriesAsync(page, pageSize, level);
        return Ok(result);
    }

    /// <summary>
    /// Get the full active category tree for navigation menus.
    /// </summary>
    [HttpGet("tree")]
    public async Task<IActionResult> GetCategoryTree()
    {
        var result = await _categoryStorefrontService.GetCategoryTreeAsync();
        return Ok(result);
    }

    /// <summary>
    /// Get a single active category by ID, including its direct subcategories.
    /// </summary>
    [HttpGet("{categoryId:long}")]
    public async Task<IActionResult> GetCategoryById(long categoryId)
    {
        var result = await _categoryStorefrontService.GetCategoryByIdAsync(categoryId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }
}
