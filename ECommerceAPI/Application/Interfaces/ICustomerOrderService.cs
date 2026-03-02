using ECommerceAPI.Application.DTOs.Orders;

namespace ECommerceAPI.Application.Interfaces;

public interface ICustomerOrderService
{
    Task<CustomerOrderListResponseDto> GetMyOrdersAsync(Guid customerId, int page, int pageSize, short? status = null);
    Task<CustomerOrderDetailResponseDto> GetOrderByIdAsync(Guid customerId, Guid orderId);
    Task<OrderTrackingDto?> GetOrderTrackingAsync(Guid customerId, Guid orderId);
    Task<ConfirmOrderResponseDto> ConfirmOrderAsync(Guid customerId, Guid orderId);
}

