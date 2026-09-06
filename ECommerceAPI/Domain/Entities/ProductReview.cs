using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class ProductReview
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Guid UserId { get; set; }

    public short Rating { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public List<string> ImageUrls { get; set; } = new();

    /// <summary>Phản hồi của shop (nếu có).</summary>
    public string? SellerReply { get; set; }

    public short Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
