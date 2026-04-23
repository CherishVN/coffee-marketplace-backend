using ECommerceAPI.Application.DTOs.Orders;

namespace ECommerceAPI.Application.Interfaces;

public interface ICustomerOrderService
{
    Task<CustomerOrderListResponseDto> GetMyOrdersAsync(Guid customerId, int page, int pageSize, short? status = null);
    Task<CustomerOrderDetailResponseDto> GetOrderByIdAsync(Guid customerId, Guid orderId);
    Task<OrderTrackingDto?> GetOrderTrackingAsync(Guid customerId, Guid orderId);
    Task<ConfirmOrderResponseDto> ConfirmOrderAsync(Guid customerId, Guid orderId);
    /// <summary>Đơn Delivered đủ điều kiện → Completed (hệ thống). Dùng background job.</summary>
    Task<int> AutoCompleteDeliveredOrdersPastDisputeWindowAsync(CancellationToken cancellationToken = default);
    Task<ServiceResponse> CancelOrderAsync(Guid customerId, Guid orderId, string? reason = null);
    Task<ServiceResponse> CancelPendingOrderAsync(Guid customerId, Guid orderId, string? reason = null);
}

