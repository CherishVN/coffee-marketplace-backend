using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public class FavoriteController : ControllerBase
{
    private readonly IFavoriteService _favoriteService;
    private readonly IUserClaimsService _userClaimsService;

    public FavoriteController(IFavoriteService favoriteService, IUserClaimsService userClaimsService)
    {
        _favoriteService = favoriteService;
        _userClaimsService = userClaimsService;
    }

    /// <summary>
    /// Get all favorited product IDs of the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = _userClaimsService.GetUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var ids = await _favoriteService.GetFavoriteIdsAsync(userId.Value);
        return Ok(new { success = true, productIds = ids });
    }

    /// <summary>
    /// Toggle favorite for a product (add if not favorited, remove if already favorited)
    /// </summary>
    [HttpPost("{productId:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid productId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var isFavorited = await _favoriteService.ToggleFavoriteAsync(userId.Value, productId);
            return Ok(new { success = true, isFavorited });
        }
        catch
        {
            return BadRequest(new { success = false, message = "Không tìm thấy sản phẩm" });
        }
    }

    /// <summary>
    /// Check if a specific product is favorited
    /// </summary>
    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> CheckFavorite(Guid productId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var isFavorited = await _favoriteService.IsFavoritedAsync(userId.Value, productId);
        return Ok(new { success = true, isFavorited });
    }
}
