using ECommerceAPI.Application.DTOs.Admin;

namespace ECommerceAPI.Application.Interfaces;

public interface IOrderAdminService
{
    Task<AdminOrderListResponseDto> GetAllOrdersAsync(
        int page,
        int pageSize,
        short? status = null,
        Guid? shopId = null,
        Guid? customerId = null,
        string? search = null);

    Task<AdminOrderResponseDto> GetOrderByIdAsync(Guid orderId);
    Task<AdminOrderResponseDto> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusDto dto, Guid adminId);
}
