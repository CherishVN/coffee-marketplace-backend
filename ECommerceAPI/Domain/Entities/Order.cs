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

    /// <summary>Phí thực tế trả cho GHN</summary>
    public decimal ProviderShippingFee { get; set; } = 0;

    /// <summary>Đối tác vận chuyển (VD: 'GHN')</summary>
    public string? ShippingProvider { get; set; }

    /// <summary>Gói dịch vụ (VD: '53320')</summary>
    public string? ShippingServiceId { get; set; }

    /// <summary>Mã vận đơn GHN</summary>
    public string? TrackingCode { get; set; }

    /// <summary>Dự kiến giao hàng</summary>
    public DateTimeOffset? EstimatedDeliveryDate { get; set; }

    /// <summary>Thời gian giao thực tế</summary>
    public DateTimeOffset? ActualDeliveryDate { get; set; }

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Address? ShippingAddress { get; set; }

    public virtual Shop Shop { get; set; } = null!;

    public virtual ICollection<ShopReview> ShopReviews { get; set; } = new List<ShopReview>();

    public virtual Transaction? Transaction { get; set; }

    // Dispute (mỗi order chỉ có tối đa 1 dispute)
    public virtual Dispute? Dispute { get; set; }
}
