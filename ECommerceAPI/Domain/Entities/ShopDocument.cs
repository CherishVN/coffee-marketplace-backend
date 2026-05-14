using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class ShopDocument
{
    public Guid Id { get; set; }

    public Guid ShopId { get; set; }

    public string DocType { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public short Status { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual Shop Shop { get; set; } = null!;
}
