using ECommerceAPI.Application.DTOs.Cart;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;
    private readonly IUserClaimsService _userClaimsService;

    public CartController(ICartService cartService, IUserClaimsService userClaimsService)
    {
        _cartService = cartService;
        _userClaimsService = userClaimsService;
    }

    /// <summary>Xem giỏ hàng của tôi</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyCart()
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null) return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var cart = await _cartService.GetMyCartAsync(userId.Value);
        if (cart == null)
            return Ok(new { success = true, data = new { id = (Guid?)null, items = Array.Empty<object>(), subtotal = 0, totalItems = 0 } });

        return Ok(new { success = true, data = cart });
    }

    /// <summary>Thêm sản phẩm vào giỏ</summary>
    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null) return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var (success, error, item) = await _cartService.AddItemAsync(userId.Value, dto);
        if (!success) return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, message = "Đã thêm vào giỏ hàng", data = item });
    }

    /// <summary>Cập nhật số lượng sản phẩm trong giỏ</summary>
    [HttpPut("items/{itemId}")]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] UpdateCartItemDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null) return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var (success, error) = await _cartService.UpdateItemAsync(userId.Value, itemId, dto);
        if (!success) return BadRequest(new { success = false, message = error });

        return Ok(new { success = true, message = "Đã cập nhật số lượng" });
    }

    /// <summary>Xóa sản phẩm khỏi giỏ</summary>
    [HttpDelete("items/{itemId}")]
    public async Task<IActionResult> RemoveItem(Guid itemId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null) return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var (success, error) = await _cartService.RemoveItemAsync(userId.Value, itemId);
        if (!success) return NotFound(new { success = false, message = error });

        return Ok(new { success = true, message = "Đã xóa khỏi giỏ hàng" });
    }

    /// <summary>Checkout - tạo đơn hàng từ giỏ hàng</summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null) return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _cartService.CheckoutAsync(userId.Value, dto);
        if (!result.Success) return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result });
    }
}
