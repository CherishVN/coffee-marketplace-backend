using System;

namespace ECommerceAPI.Domain.Entities;

public partial class ShopFollow
{
    public Guid UserId { get; set; }

    public Guid ShopId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Shop Shop { get; set; } = null!;
}
