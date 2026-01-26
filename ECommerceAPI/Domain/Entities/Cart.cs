using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Cart
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public short Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual User Customer { get; set; } = null!;
}
