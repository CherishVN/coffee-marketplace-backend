using System.Security.Claims;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "admin")]
public class AdminOrderController : ControllerBase
{
    private readonly IOrderAdminService _orderAdminService;

    public AdminOrderController(IOrderAdminService orderAdminService)
    {
        _orderAdminService = orderAdminService;
    }

    /// <summary>
    /// Get all orders for admin management
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? status = null,
        [FromQuery] Guid? shopId = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? search = null)
    {
        var result = await _orderAdminService.GetAllOrdersAsync(page, pageSize, status, shopId, customerId, search);
        return Ok(result);
    }

    /// <summary>
    /// Get order details
    /// </summary>
    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrderById(Guid orderId)
    {
        var result = await _orderAdminService.GetOrderByIdAsync(orderId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Update order status
    /// </summary>
    [HttpPut("{orderId}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _orderAdminService.UpdateOrderStatusAsync(orderId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
