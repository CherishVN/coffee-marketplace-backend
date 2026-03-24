using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

/// <summary>
/// Public product endpoints — no authentication required.
/// Used by the customer-facing storefront.
/// </summary>
[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly IProductStorefrontService _productStorefrontService;

    public ProductController(IProductStorefrontService productStorefrontService)
    {
        _productStorefrontService = productStorefrontService;
    }

    /// <summary>
    /// Get paginated list of active products.
    /// Supports filtering by category, price range, keyword search and sorting.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? sortBy = null)
    {
        var result = await _productStorefrontService.GetProductsAsync(
            page, pageSize, categoryId, search, minPrice, maxPrice, sortBy);
        return Ok(result);
    }

    /// <summary>
    /// Get product detail by ID. Only returns active products.
    /// </summary>
    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> GetProductById(Guid productId)
    {
        var result = await _productStorefrontService.GetProductByIdAsync(productId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetProductBySlug(string slug)
    {
        var result = await _productStorefrontService.GetProductBySlugAsync(slug);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }
}
