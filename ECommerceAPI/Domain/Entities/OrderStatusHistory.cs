using System;

namespace ECommerceAPI.Domain.Entities;

public partial class OrderStatusHistory
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    /// <summary>Trạng thái trước khi đổi (null nếu là bước đầu, ví dụ tạo đơn).</summary>
    public short? PreviousStatus { get; set; }

    public short NewStatus { get; set; }

    public Guid? ChangedBy { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual User? ChangedByUser { get; set; }
}
