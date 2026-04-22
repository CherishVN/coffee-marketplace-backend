using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class Order
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Guid ShopId { get; set; }

    public string OrderCode { get; set; } = null!;

    public short Status { get; set; }

    public decimal Subtotal { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal Total { get; set; }

    public string? ShipFullName { get; set; }

    public string? ShipPhone { get; set; }

    public string? ShipAddress { get; set; }

    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? ShippingAddressId { get; set; }

    public Guid? TransactionId { get; set; }

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Address? ShippingAddress { get; set; }

    public virtual Shop Shop { get; set; } = null!;



    public virtual Transaction? Transaction { get; set; }

    // Dispute (mỗi order chỉ có tối đa 1 dispute)
    public virtual Dispute? Dispute { get; set; }

    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();
}
