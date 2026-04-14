using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

/// <summary>
/// Khiếu nại từ khách hàng về đơn hàng
/// </summary>
public partial class Dispute
{
    public Guid Id { get; set; }

    /// <summary>
    /// Đơn hàng bị khiếu nại
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Khách hàng tạo khiếu nại
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Shop bị khiếu nại
    /// </summary>
    public Guid ShopId { get; set; }

    /// <summary>
    /// Loại khiếu nại: 0=refund, 1=return, 2=damaged, 3=not_received, 4=wrong_item, 5=quality_issue, 6=other
    /// </summary>
    public short Type { get; set; }

    /// <summary>
    /// Trạng thái: 0=pending, 1=under_review, 2=waiting_seller, 3=waiting_customer, 4=resolved, 5=rejected, 6=refunded, 7=cancelled
    /// </summary>
    public short Status { get; set; }

    /// <summary>
    /// Tiêu đề khiếu nại
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Lý do khiếu nại chi tiết
    /// </summary>
    public string Reason { get; set; } = null!;

    /// <summary>
    /// URLs ảnh/video bằng chứng từ customer (JSON array)
    /// </summary>
    public string? EvidenceUrls { get; set; }

    /// <summary>
    /// Số tiền yêu cầu hoàn
    /// </summary>
    public decimal RequestedAmount { get; set; }

    /// <summary>
    /// Số tiền được duyệt hoàn
    /// </summary>
    public decimal? ApprovedAmount { get; set; }

    /// <summary>
    /// Phản hồi từ seller
    /// </summary>
    public string? SellerResponse { get; set; }

    /// <summary>
    /// URLs bằng chứng từ seller (JSON array)
    /// </summary>
    public string? SellerEvidenceUrls { get; set; }

    /// <summary>
    /// Thời điểm seller phản hồi
    /// </summary>
    public DateTime? SellerRespondedAt { get; set; }

    /// <summary>
    /// Quyết định giải quyết
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    /// Phản hồi bổ sung từ customer (khi admin yêu cầu thêm thông tin)
    /// </summary>
    public string? CustomerNote { get; set; }

    /// <summary>
    /// Ghi chú từ admin
    /// </summary>
    public string? AdminNote { get; set; }

    /// <summary>
    /// Admin xử lý khiếu nại
    /// </summary>
    public Guid? ResolvedBy { get; set; }

    /// <summary>
    /// Thời điểm giải quyết
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public virtual Order Order { get; set; } = null!;
    public virtual User Customer { get; set; } = null!;
    public virtual Shop Shop { get; set; } = null!;
    public virtual User? ResolvedByNavigation { get; set; }
    public virtual ICollection<DisputeMessage> DisputeMessages { get; set; } = new List<DisputeMessage>();
}
