namespace ECommerceAPI.Domain.Entities;

/// <summary>
/// Dòng hàng trong đơn bị khiếu nại (số lượng có thể &lt; dòng gốc nếu chỉ một phần lỗi).
/// </summary>
public partial class DisputeOrderItem
{
    public Guid Id { get; set; }

    public Guid DisputeId { get; set; }

    public Guid OrderItemId { get; set; }

    /// <summary>Số lượng sản phẩm khiếu nại trên dòng này (1..OrderItem.Quantity).</summary>
    public int Quantity { get; set; }

    /// <summary>Đơn giá tại thời điểm đặt hàng (snapshot).</summary>
    public decimal UnitPriceSnapshot { get; set; }

    /// <summary>Thành tiền phần khiếu nại (thường = UnitPriceSnapshot × Quantity).</summary>
    public decimal LineSnapshotTotal { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Dispute Dispute { get; set; } = null!;

    public virtual OrderItem OrderItem { get; set; } = null!;
}
