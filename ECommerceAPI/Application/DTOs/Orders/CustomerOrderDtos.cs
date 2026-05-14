using ECommerceAPI.Application;
using ECommerceAPI.Domain.Enums;

namespace ECommerceAPI.Application.DTOs.Orders;

public class OrderStatusStepDto
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public short Value { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime? ReachedAt { get; set; }
}

public class OrderStatusHistoryItemDto
{
    public short? PreviousStatus { get; set; }
    public string? PreviousStatusLabel { get; set; }
    public short NewStatus { get; set; }
    public string NewStatusLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
    /// <summary>system, customer, shop, other</summary>
    public string Source { get; set; } = "system";
}

public class CustomerOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool HasReviewedByUser { get; set; }
}

public class CustomerOrderSummaryDto
{
    public Guid Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string? PaymentProvider { get; set; }
    public string? CancelReason { get; set; }
    public Guid ShopId { get; set; }
    public string ShopSlug { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public short Status { get; set; }
    public string StatusName => OrderStatusVnHelper.Vietnamese((OrderStatus)Status);
    public DateTime CreatedAt { get; set; }
    public List<CustomerOrderItemDto> Items { get; set; } = new();
}

public class CustomerOrderDetailDto : CustomerOrderSummaryDto
{
    public string? ShipFullName { get; set; }
    public string? ShipPhone { get; set; }
    public string? ShipAddress { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderStatusHistoryItemDto> StatusHistory { get; set; } = new();
    public List<OrderStatusStepDto> StatusTimeline { get; set; } = new();
    /// <summary>Ngày giao hàng dự kiến (lấy từ vận đơn mới nhất).</summary>
    public DateTimeOffset? EstimatedDeliveryDate { get; set; }
    /// <summary>Ngày giao hàng thực tế (nếu đã giao).</summary>
    public DateTimeOffset? ActualDeliveryDate { get; set; }
    /// <summary>Mã vận đơn (tracking code) để khách tra cứu.</summary>
    public string? TrackingCode { get; set; }
    /// <summary>Đơn vị vận chuyển (GHN, ...).</summary>
    public string? ShippingProvider { get; set; }
    /// <summary>Danh sách URL ảnh bằng chứng giao hàng.</summary>
    public List<string>? DeliveryProofUrls { get; set; }
    /// <summary>
    /// Thời điểm khách gửi yêu cầu hủy đang chờ shop duyệt.
    /// Null = không có yêu cầu hủy đang chờ.
    /// </summary>
    public DateTimeOffset? CancelRequestedAt { get; set; }
    /// <summary>Hạn shop phải phê duyệt / từ chối (= CancelRequestedAt + timeout).</summary>
    public DateTimeOffset? CancelRequestDeadline { get; set; }
}

/// <summary>Response cho API hủy đơn — phân biệt hủy ngay vs yêu cầu chờ duyệt.</summary>
public class CancelOrderResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>true = đơn đã hủy ngay; false = yêu cầu hủy đã gửi đến shop, chờ duyệt.</summary>
    public bool CancelledImmediately { get; set; }
    /// <summary>Thời điểm yêu cầu hủy được tạo (khi CancelledImmediately = false).</summary>
    public DateTimeOffset? CancelRequestedAt { get; set; }
    /// <summary>Hạn shop phải phản hồi.</summary>
    public DateTimeOffset? CancelRequestDeadline { get; set; }
}

public class OrderTrackingDto
{
    public Guid OrderId { get; set; }
    public short CurrentStatus { get; set; }
    public string CurrentStatusName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderStatusStepDto> Timeline { get; set; } = new();
}

public class CustomerOrderListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CustomerOrderSummaryDto> Orders { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CustomerOrderDetailResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public CustomerOrderDetailDto? Order { get; set; }
}

public class ConfirmOrderResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public short NewStatus { get; set; }
    public string NewStatusName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class CancelOrderRequestDto
{
    public string? Reason { get; set; }
}

