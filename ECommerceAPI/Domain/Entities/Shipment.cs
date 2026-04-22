using System;

namespace ECommerceAPI.Domain.Entities;

/// <summary>
/// Một vận đơn (GHN/đối tác) gắn với đơn hàng — trạng thái vận chuyển thô từ hãng.
/// </summary>
public partial class Shipment
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid ShopId { get; set; }

    public string ShippingProvider { get; set; } = "GHN";

    public string? ShippingServiceId { get; set; }

    public string TrackingCode { get; set; } = null!;

    /// <summary>Trạng thái vận đơn thô từ GHN (vd. ready_to_pick, delivered).</summary>
    public string Status { get; set; } = null!;

    public decimal ProviderShippingFee { get; set; }

    public decimal CodAmount { get; set; }

    public DateTimeOffset? EstimatedDeliveryDate { get; set; }

    public DateTimeOffset? ActualDeliveryDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Shop Shop { get; set; } = null!;
}
