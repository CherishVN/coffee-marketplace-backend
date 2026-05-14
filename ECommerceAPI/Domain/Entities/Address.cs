using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Address
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? Label { get; set; }

    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public string AddressLine1 { get; set; } = null!;

    public string? AddressLine2 { get; set; }

    public string? Ward { get; set; }

    public string? District { get; set; }

    public string City { get; set; } = null!;

    public string? Province { get; set; }

    public string? PostalCode { get; set; }

    public string Country { get; set; } = null!;

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual User User { get; set; } = null!;
}
