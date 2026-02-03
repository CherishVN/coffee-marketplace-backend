using System.Security.Claims;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/sellers")]
[Authorize(Roles = "admin")]
public class AdminSellerController : ControllerBase
{
    private readonly ISellerApprovalService _sellerApprovalService;

    public AdminSellerController(ISellerApprovalService sellerApprovalService)
    {
        _sellerApprovalService = sellerApprovalService;
    }

    [HttpGet]
    public async Task<IActionResult> GetShops(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] short? verificationStatus = null)
    {
        var result = await _sellerApprovalService.GetPendingShopsAsync(page, pageSize, verificationStatus);
        return Ok(result);
    }

    [HttpGet("{shopId}")]
    public async Task<IActionResult> GetShopById(Guid shopId)
    {
        var result = await _sellerApprovalService.GetShopByIdAsync(shopId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpPost("{shopId}/approve")]
    public async Task<IActionResult> ApproveShop(Guid shopId, [FromBody] ApproveSellerDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _sellerApprovalService.ApproveShopAsync(shopId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{shopId}/reject")]
    public async Task<IActionResult> RejectShop(Guid shopId, [FromBody] RejectSellerDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _sellerApprovalService.RejectShopAsync(shopId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{shopId}/activate")]
    public async Task<IActionResult> ActivateShop(Guid shopId)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _sellerApprovalService.ActivateShopAsync(shopId, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{shopId}/suspend")]
    public async Task<IActionResult> SuspendShop(Guid shopId, [FromBody] SuspendShopDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _sellerApprovalService.SuspendShopAsync(shopId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{shopId}/close")]
    public async Task<IActionResult> CloseShop(Guid shopId, [FromBody] SuspendShopDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _sellerApprovalService.CloseShopAsync(shopId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
