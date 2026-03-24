using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Shop
{
    public Guid Id { get; set; }

    public string ShopCode { get; set; } = null!;

    public Guid OwnerId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public string? LogoUrl { get; set; }

    public short Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public short VerificationStatus { get; set; }

    public string? RejectionReason { get; set; }

    public string? SuspensionReason { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public Guid? SuspendedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public Guid? VerifiedBy { get; set; }

    public string? CoverUrl { get; set; }

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual User Owner { get; set; } = null!;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<ShopDocument> ShopDocuments { get; set; } = new List<ShopDocument>();

    public virtual ICollection<ShopFollow> ShopFollows { get; set; } = new List<ShopFollow>();

    public virtual ICollection<ShopReview> ShopReviews { get; set; } = new List<ShopReview>();

    public virtual User? VerifiedByNavigation { get; set; }

    // Disputes
    public virtual ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
}
