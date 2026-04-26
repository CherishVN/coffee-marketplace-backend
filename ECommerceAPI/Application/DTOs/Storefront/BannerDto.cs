using System;

namespace ECommerceAPI.Application.DTOs.Storefront;

public class BannerDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Subtitle { get; set; }
    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }
    public string ImageDesktop { get; set; } = null!;
    public string? ImageMobile { get; set; }
    public string? ImageAlt { get; set; }
    public string? TextPosition { get; set; }
    public string? TextColor { get; set; }
    public decimal? OverlayOpacity { get; set; }
    public string? LinkType { get; set; }
    public string? LinkValue { get; set; }
}
