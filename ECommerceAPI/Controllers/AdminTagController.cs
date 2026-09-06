using System.Security.Claims;
using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/admin/tags")]
[Authorize(Roles = "admin")]
public class AdminTagController : ControllerBase
{
    private readonly ITagAdminService _tagAdminService;

    public AdminTagController(ITagAdminService tagAdminService)
    {
        _tagAdminService = tagAdminService;
    }

    /// <summary>
    /// Get all tags with pagination and search
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllTags(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
        var result = await _tagAdminService.GetAllTagsAsync(page, pageSize, search, isActive);
        return Ok(result);
    }

    /// <summary>
    /// Get tag by ID
    /// </summary>
    [HttpGet("{tagId}")]
    public async Task<IActionResult> GetTagById(long tagId)
    {
        var result = await _tagAdminService.GetTagByIdAsync(tagId);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Create new tag
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _tagAdminService.CreateTagAsync(dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return CreatedAtAction(nameof(GetTagById), new { tagId = result.Tag?.Id }, result);
    }

    /// <summary>
    /// Update existing tag
    /// </summary>
    [HttpPut("{tagId}")]
    public async Task<IActionResult> UpdateTag(long tagId, [FromBody] UpdateTagDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _tagAdminService.UpdateTagAsync(tagId, dto, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Delete tag (only if not in use)
    /// </summary>
    [HttpDelete("{tagId}")]
    public async Task<IActionResult> DeleteTag(long tagId)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _tagAdminService.DeleteTagAsync(tagId, adminId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
