using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;


[ApiController]
[Route("api/shops")]
public class ShopController : ControllerBase
{
    private readonly IShopStorefrontService _shopService;
    private readonly IUserClaimsService _userClaimsService;

    public ShopController(IShopStorefrontService shopService, IUserClaimsService userClaimsService)
    {
        _shopService = shopService;
        _userClaimsService = userClaimsService;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetShopBySlug(string slug)
    {
        Guid? currentUserId = null;
        try { currentUserId = _userClaimsService.GetUserId(); } catch { }

        var result = await _shopService.GetShopBySlugAsync(slug, currentUserId);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpGet("{shopId:guid}/products")]
    public async Task<IActionResult> GetShopProducts(
        Guid shopId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] long? categoryId = null)
    {
        var result = await _shopService.GetShopProductsAsync(shopId, page, pageSize, sortBy, categoryId);
        return Ok(result);
    }

    [HttpGet("{shopId:guid}/categories")]
    public async Task<IActionResult> GetShopCategories(Guid shopId)
    {
        var result = await _shopService.GetShopCategoriesAsync(shopId);
        return Ok(new { success = true, categories = result });
    }

    [HttpPost("{shopId:guid}/follow")]
    [Authorize]
    public async Task<IActionResult> FollowShop(Guid shopId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _shopService.FollowShopAsync(userId.Value, shopId);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    [HttpDelete("{shopId:guid}/follow")]
    [Authorize]
    public async Task<IActionResult> UnfollowShop(Guid shopId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _shopService.UnfollowShopAsync(userId.Value, shopId);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }
    [HttpGet("followed")]
    [Authorize]
    public async Task<IActionResult> GetFollowedShops()
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var shops = await _shopService.GetFollowedShopsAsync(userId.Value);
        return Ok(new { success = true, shops });
    }
}
