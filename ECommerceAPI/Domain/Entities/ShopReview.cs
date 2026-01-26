using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class ShopReview
{
    public Guid Id { get; set; }

    public Guid ShopId { get; set; }

    public Guid UserId { get; set; }

    public Guid? OrderId { get; set; }

    public short Rating { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public short Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Shop Shop { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
