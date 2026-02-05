using System;

namespace ECommerceAPI.Domain.Entities;

/// <summary>
/// Tin nhắn trao đổi trong khiếu nại
/// </summary>
public partial class DisputeMessage
{
    public Guid Id { get; set; }

    /// <summary>
    /// Khiếu nại liên quan
    /// </summary>
    public Guid DisputeId { get; set; }

    /// <summary>
    /// Người gửi
    /// </summary>
    public Guid SenderId { get; set; }

    /// <summary>
    /// Vai trò người gửi: 0=customer, 1=seller, 2=admin
    /// </summary>
    public short SenderRole { get; set; }

    /// <summary>
    /// Nội dung tin nhắn
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// URLs đính kèm (JSON array)
    /// </summary>
    public string? Attachments { get; set; }

    /// <summary>
    /// Đã đọc chưa
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Thời điểm đọc
    /// </summary>
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual Dispute Dispute { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
}
