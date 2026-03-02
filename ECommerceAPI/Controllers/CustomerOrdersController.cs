using ECommerceAPI.Application.DTOs.Orders;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class CustomerOrdersController : ControllerBase
{
    private readonly ICustomerOrderService _customerOrderService;
    private readonly IUserClaimsService _userClaimsService;

    public CustomerOrdersController(
        ICustomerOrderService customerOrderService,
        IUserClaimsService userClaimsService)
    {
        _customerOrderService = customerOrderService;
        _userClaimsService = userClaimsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? status = null)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _customerOrderService.GetMyOrdersAsync(userId.Value, page, pageSize, status);
        return Ok(result);
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrderById(Guid orderId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _customerOrderService.GetOrderByIdAsync(userId.Value, orderId);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpGet("{orderId}/tracking")]
    public async Task<IActionResult> GetOrderTracking(Guid orderId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var tracking = await _customerOrderService.GetOrderTrackingAsync(userId.Value, orderId);
        if (tracking == null)
            return NotFound(new { success = false, message = "Không tìm thấy đơn hàng" });

        return Ok(new { success = true, data = tracking });
    }

    [HttpPost("{orderId}/confirm")]
    public async Task<IActionResult> ConfirmOrder(Guid orderId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _customerOrderService.ConfirmOrderAsync(userId.Value, orderId);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}

