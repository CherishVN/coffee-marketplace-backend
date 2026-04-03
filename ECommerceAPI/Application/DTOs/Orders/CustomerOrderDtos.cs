using ECommerceAPI.Domain.Enums;

namespace ECommerceAPI.Application.DTOs.Orders;

public class OrderStatusStepDto
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public short Value { get; set; }
    public string State { get; set; } = string.Empty; // completed, current, upcoming, cancelled
    public DateTime? ReachedAt { get; set; }
}

public class CustomerOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? ThumbnailUrl { get; set; }
    /// <summary>Đã có bản ghi đánh giá sản phẩm của user (product_reviews: unique theo product + user).</summary>
    public bool HasReviewedByUser { get; set; }
}

public class CustomerOrderSummaryDto
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string ShopSlug { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public short Status { get; set; }
    public string StatusName => ((OrderStatus)Status).ToString();
    public DateTime CreatedAt { get; set; }
    public List<CustomerOrderItemDto> Items { get; set; } = new();
}

public class CustomerOrderDetailDto : CustomerOrderSummaryDto
{
    public string? ShipFullName { get; set; }
    public string? ShipPhone { get; set; }
    public string? ShipAddress { get; set; }
    public List<CustomerOrderItemDto> Items { get; set; } = new();
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

