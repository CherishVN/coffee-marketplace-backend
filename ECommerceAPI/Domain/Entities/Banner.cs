using System;

namespace ECommerceAPI.Domain.Entities;

public partial class Banner
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;
    public string? Subtitle { get; set; }

    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }

    public string ImageDesktop { get; set; } = null!;
    public string? ImageMobile { get; set; }
    public string? ImageAlt { get; set; }

    public string? TextPosition { get; set; } = "left";
    public string? TextColor { get; set; } = "light";
    public decimal? OverlayOpacity { get; set; } = 0.30m;

    public string? LinkType { get; set; } = "collection";
    public string? LinkValue { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
