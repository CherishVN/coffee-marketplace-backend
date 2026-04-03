using ECommerceAPI.Application.DTOs.Seller;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/seller")]
[Authorize(Roles = "seller,admin")]
public class SellerController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly IUserClaimsService _userClaimsService;

    public SellerController(ISellerService sellerService, IUserClaimsService userClaimsService)
    {
        _sellerService = sellerService;
        _userClaimsService = userClaimsService;
    }

    // ==================== SHOP MANAGEMENT ====================

    /// <summary>
    /// Get my shop info
    /// </summary>
    [HttpGet("shop")]
    public async Task<IActionResult> GetMyShop()
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.GetMyShopAsync(userId.Value);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// Update shop info
    /// </summary>
    [HttpPut("shop")]
    public async Task<IActionResult> UpdateShop([FromBody] UpdateShopDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.UpdateShopAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    // ==================== WALLET & WITHDRAWAL ====================

    /// <summary>
    /// Get my wallet info
    /// </summary>
    [HttpGet("wallet")]
    public async Task<IActionResult> GetMyWallet()
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.GetMyWalletAsync(userId.Value);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// Get my withdrawal requests
    /// </summary>
    [HttpGet("withdrawals")]
    public async Task<IActionResult> GetMyWithdrawals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.GetMyWithdrawalRequestsAsync(userId.Value, page, pageSize);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// Create withdrawal request
    /// </summary>
    [HttpPost("withdrawals")]
    public async Task<IActionResult> CreateWithdrawal([FromBody] CreateWithdrawalRequestDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.CreateWithdrawalRequestAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    // ==================== PRODUCT MANAGEMENT ====================

    /// <summary>
    /// Get all my products
    /// </summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetMyProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? status = null,
        [FromQuery] string? search = null)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.GetMyProductsAsync(userId.Value, page, pageSize, status, search);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data, totalCount = result.TotalCount });
    }

    /// <summary>
    /// Get product by ID
    /// </summary>
    [HttpGet("products/{productId}")]
    public async Task<IActionResult> GetProductById(Guid productId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.GetProductByIdAsync(userId.Value, productId);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// Create new product
    /// </summary>
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.CreateProductAsync(userId.Value, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = "Tạo sản phẩm thành công", data = result.Data });
    }

    /// <summary>
    /// Update product
    /// </summary>
    [HttpPut("products/{productId}")]
    public async Task<IActionResult> UpdateProduct(Guid productId, [FromBody] UpdateProductDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.UpdateProductAsync(userId.Value, productId, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    /// <summary>
    /// Delete product (soft delete)
    /// </summary>
    [HttpDelete("products/{productId}")]
    public async Task<IActionResult> DeleteProduct(Guid productId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.DeleteProductAsync(userId.Value, productId);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    /// <summary>
    /// Thêm biến thể cho sản phẩm đã tồn tại
    /// </summary>
    [HttpPost("products/{productId}/variants")]
    public async Task<IActionResult> AddProductVariant(Guid productId, [FromBody] ProductVariantDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.AddProductVariantAsync(userId.Value, productId, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = "Đã thêm biến thể", data = result.Data });
    }

    // ==================== INVENTORY MANAGEMENT ====================

    /// <summary>
    /// Update product inventory
    /// </summary>
    [HttpPut("products/{productId}/inventory")]
    public async Task<IActionResult> UpdateInventory(Guid productId, [FromBody] UpdateInventoryDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.UpdateInventoryAsync(userId.Value, productId, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    // ==================== ORDER MANAGEMENT ====================

    /// <summary>
    /// Get all my orders
    /// </summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? status = null)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.GetMyOrdersAsync(userId.Value, page, pageSize, status);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// Get order by ID
    /// </summary>
    [HttpGet("orders/{orderId}")]
    public async Task<IActionResult> GetOrderById(Guid orderId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.GetOrderByIdAsync(userId.Value, orderId);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }

    /// <summary>
    /// Update order status
    /// </summary>
    [HttpPut("orders/{orderId}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] SellerUpdateOrderStatusDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.UpdateOrderStatusAsync(userId.Value, orderId, dto);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message });
    }

    /// <summary>
    /// Đánh giá sản phẩm từ khách (theo sản phẩm thuộc shop)
    /// </summary>
    [HttpGet("reviews")]
    public async Task<IActionResult> GetMyProductReviews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? rating = null,
        [FromQuery] string? search = null)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerService.GetMyProductReviewsAsync(userId.Value, page, pageSize, rating, search);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, data = result.Data });
    }
}
