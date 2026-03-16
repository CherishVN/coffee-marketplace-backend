using ECommerceAPI.Application.DTOs.Reviews;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly IUserClaimsService _userClaimsService;

    public ReviewsController(IReviewService reviewService, IUserClaimsService userClaimsService)
    {
        _reviewService = reviewService;
        _userClaimsService = userClaimsService;
    }

    /// <summary>
    /// Create product review for completed order
    /// </summary>
    [HttpPost("products")]
    [Authorize]
    public async Task<IActionResult> CreateProductReview([FromBody] CreateProductReviewDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _reviewService.CreateProductReviewAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    /// <summary>
    /// Get approved reviews of a product
    /// </summary>
    [HttpGet("products/{productId}")]
    public async Task<IActionResult> GetProductReviews(
        Guid productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "newest")
    {
        var result = await _reviewService.GetProductReviewsAsync(productId, page, pageSize, sortBy);
        return Ok(result);
    }

    /// <summary>
    /// Create shop review for completed order
    /// </summary>
    [HttpPost("shops")]
    [Authorize]
    public async Task<IActionResult> CreateShopReview([FromBody] CreateShopReviewDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _reviewService.CreateShopReviewAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    /// <summary>
    /// Get approved reviews of a shop (public)
    /// </summary>
    [HttpGet("shops/{shopId}")]
    public async Task<IActionResult> GetShopReviews(
        Guid shopId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "newest")
    {
        var result = await _reviewService.GetShopReviewsAsync(shopId, page, pageSize, sortBy);
        return Ok(result);
    }
}

