using ECommerceAPI.Application.DTOs.Disputes;

namespace ECommerceAPI.Application.DTOs.Admin;

public class DisputeAdminDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal OrderTotal { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public short Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public short Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? SellerResponse { get; set; }
    public DateTime? SellerRespondedAt { get; set; }
    public string? Resolution { get; set; }
    public string? AdminNote { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> EvidenceUrls { get; set; } = new();
    public List<string> SellerEvidenceUrls { get; set; } = new();
    public string? CustomerNote { get; set; }
    public List<DisputeAffectedItemDto> AffectedItems { get; set; } = new();
}

public class ApproveRefundDto
{
    public decimal? ApprovedAmount { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
}

public class RequestResponseDto
{
    /// <summary>Tin nhắn/hướng dẫn gửi kèm cho bên được yêu cầu phản hồi</summary>
    public string? AdminNote { get; set; }
}

public class RejectDisputeDto
{
    public string Resolution { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
}

public class DisputeListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<DisputeAdminDto> Disputes { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class DisputeResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DisputeAdminDto? Dispute { get; set; }
}
