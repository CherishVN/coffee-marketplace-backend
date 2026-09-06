using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECommerceAPI.Infrastructure.Data;
using ECommerceAPI.Application.DTOs.Storefront;

namespace ECommerceAPI.Controllers;

[ApiController]
[Route("api/home")]
public class HomeController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HomeResponseDto>> GetHomeData()
    {
        var now = DateTime.UtcNow;

        var banners = await _context.Banners
            .Where(b => b.IsActive
                     && (b.StartsAt == null || b.StartsAt <= now)
                     && (b.EndsAt   == null || b.EndsAt   >= now))
            .OrderBy(b => b.SortOrder)
            .Select(b => new BannerDto
            {
                Id             = b.Id,
                Title          = b.Title,
                Subtitle       = b.Subtitle,
                CtaText        = b.CtaText,
                CtaUrl         = b.CtaUrl,
                ImageDesktop   = b.ImageDesktop,
                ImageMobile    = b.ImageMobile,
                ImageAlt       = b.ImageAlt,
                TextPosition   = b.TextPosition,
                TextColor      = b.TextColor,
                OverlayOpacity = b.OverlayOpacity,
                LinkType       = b.LinkType,
                LinkValue      = b.LinkValue,
            })
            .ToListAsync();

        return Ok(new HomeResponseDto { Banners = banners });
    }
}