using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class FavoriteProduct
{
    public Guid UserId { get; set; }

    public Guid ProductId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
