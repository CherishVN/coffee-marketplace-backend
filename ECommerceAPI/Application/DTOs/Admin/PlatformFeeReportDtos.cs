namespace ECommerceAPI.Application.DTOs.Admin;

public class PlatformFeeConfigDto
{
    public Guid Id { get; set; }
    public decimal CommissionPercent { get; set; }
    public Guid ChangedBy { get; set; }
    public string? ChangedByName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdatePlatformFeeConfigRequest
{
    /// <summary>Tỷ lệ phí sàn mới (0–100).</summary>
    public decimal CommissionPercent { get; set; }

    /// <summary>Ghi chú lý do thay đổi (tuỳ chọn).</summary>
    public string? Note { get; set; }
}



public class PlatformFeeSummaryDto
{
    /// <summary>Tổng phí sàn (VND) trong khoảng lọc.</summary>
    public decimal TotalFeeAmount { get; set; }
    /// <summary>Tổng tiền hàng (subtotal) đã quyết toán.</summary>
    public decimal TotalGrossSubtotal { get; set; }
    /// <summary>Tổng đã trả cho seller (net).</summary>
    public decimal TotalNetToSeller { get; set; }
    public int RecordCount { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public class PlatformFeeRecordDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid ShopId { get; set; }
    public string? ShopName { get; set; }
    public Guid SellerId { get; set; }
    public string? SellerName { get; set; }
    public decimal GrossSubtotal { get; set; }
    public decimal CommissionPercent { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal NetToSeller { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime CreatedAt { get; set; }
}

public class PlatformFeeRecordsListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<PlatformFeeRecordDto> Records { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public PlatformFeeSummaryDto? Summary { get; set; }
}
