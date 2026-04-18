using System;
using ECommerceAPI.Application.DTOs.Disputes;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/seller/disputes")]
[Authorize(Roles = "seller,admin")]
public class SellerDisputeController : ControllerBase
{
    private readonly ISellerDisputeService _sellerDisputeService;
    private readonly IUserClaimsService _userClaimsService;

    public SellerDisputeController(
        ISellerDisputeService sellerDisputeService,
        IUserClaimsService userClaimsService)
    {
        _sellerDisputeService = sellerDisputeService;
        _userClaimsService = userClaimsService;
    }

    /// <summary>Lấy danh sách tranh chấp của shop mình</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyShopDisputes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] short? status = null,
        [FromQuery] short? type = null)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerDisputeService.GetShopDisputesAsync(userId.Value, page, pageSize, status, type);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>Lấy chi tiết 1 tranh chấp (chỉ thuộc shop mình)</summary>
    [HttpGet("{disputeId}")]
    public async Task<IActionResult> GetDisputeById(Guid disputeId)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerDisputeService.GetDisputeByIdAsync(userId.Value, disputeId);

        if (!result.Success)
        {
            if (string.Equals(result.Message, "Bạn chưa có shop", StringComparison.Ordinal))
                return BadRequest(result);
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>Seller phản hồi tranh chấp kèm bằng chứng</summary>
    [HttpPost("{disputeId}/respond")]
    public async Task<IActionResult> RespondToDispute(Guid disputeId, [FromBody] SellerRespondDisputeDto dto)
    {
        var userId = _userClaimsService.GetUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _sellerDisputeService.RespondToDisputeAsync(userId.Value, disputeId, dto);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
