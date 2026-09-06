using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Transaction
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public string? ProviderRef { get; set; }

    public short Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
