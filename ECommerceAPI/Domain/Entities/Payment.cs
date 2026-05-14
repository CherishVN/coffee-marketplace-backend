using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Payment
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string Provider { get; set; } = null!;

    public string? ProviderRef { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public short Status { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? TransactionId { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Transaction? Transaction { get; set; }
}
