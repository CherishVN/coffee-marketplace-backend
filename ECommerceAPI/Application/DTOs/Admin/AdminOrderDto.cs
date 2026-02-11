using System;
using System.Collections.Generic;

namespace ECommerceAPI.Application.DTOs.Admin;

/// <summary>
/// Order item DTO for admin view
/// </summary>
public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// Order DTO for admin list view
/// </summary>
public class AdminOrderDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? CustomerEmail { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = null!;
    public short Status { get; set; }
    public string StatusName { get; set; } = null!;
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public string? ShipFullName { get; set; }
    public string? ShipPhone { get; set; }
    public string? ShipAddress { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Detailed order DTO with items
/// </summary>
public class AdminOrderDetailDto : AdminOrderDto
{
    public List<OrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// List response with pagination
/// </summary>
public class AdminOrderListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<AdminOrderDto> Orders { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Single order response
/// </summary>
public class AdminOrderResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public AdminOrderDetailDto? Order { get; set; }
}

/// <summary>
/// DTO for updating order status
/// </summary>
public class UpdateOrderStatusDto
{
    public short NewStatus { get; set; }
    public string? Reason { get; set; }
}
