using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class OrderItem
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? VariantId { get; set; }

    public string ProductName { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ProductVariant? Variant { get; set; }

    public virtual ICollection<DisputeOrderItem> DisputeOrderItems { get; set; } = new List<DisputeOrderItem>();
}
